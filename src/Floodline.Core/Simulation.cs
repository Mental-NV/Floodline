using System;
using System.Collections.Generic;
using System.Text.Json;
using Floodline.Core.Determinism;
using Floodline.Core.Levels;
using Floodline.Core.Movement;
using Floodline.Core.Random;

namespace Floodline.Core;

/// <summary>
/// High-level orchestrator for the Floodline simulation.
/// Manages the tick pipeline and resolve phases.
/// </summary>
public sealed class Simulation
{
    private const ulong WindSeedSalt = 0x9E3779B97F4A7C15UL;
    private static readonly Int3 WindDirectionEast = new(1, 0, 0);
    private static readonly Int3 WindDirectionWest = new(-1, 0, 0);
    private static readonly Int3 WindDirectionNorth = new(0, 0, -1);
    private static readonly Int3 WindDirectionSouth = new(0, 0, 1);
    private readonly Level _level;
    private readonly IRandom _random;
    private readonly MovementController _movement;
    private readonly PieceBag _bag;
    private readonly HashSet<GravityDirection>? _allowedGravityDirections;
    private readonly WindHazard? _windHazard;
    private readonly IRandom? _windRandom;
    private readonly Dictionary<Int3, DrainConfig> _drainConfigs = [];
    private readonly IceTracker _iceTracker = new();
    private readonly List<FreezeRequest> _pendingFreezes = [];
    private readonly Dictionary<Int3, int> _stabilizeAnchorTimers = [];
    private SimulationStatus _status = SimulationStatus.InProgress;
    private long _ticksElapsed;
    private int _piecesLocked;
    internal int WaterRemovedTotal { get; private set; }
    internal int RotationsExecuted { get; private set; }

    /// <summary>
    /// Gets the current grid.
    /// </summary>
    public Grid Grid { get; }

    /// <summary>
    /// Gets the current simulation state.
    /// </summary>
    public SimulationState State => new(_status, _ticksElapsed, _piecesLocked);

    /// <summary>
    /// Gets the latest objective evaluation snapshot.
    /// </summary>
    public ObjectiveEvaluation Objectives { get; private set; } = ObjectiveEvaluation.Empty;

    /// <summary>
    /// Gets the current active piece, if any.
    /// </summary>
    public ActivePiece? ActivePiece => _movement.CurrentPiece;

    /// <summary>
    /// Gets the current gravity direction.
    /// </summary>
    public GravityDirection Gravity => _movement.Gravity;

    internal BagEntry? HoldSlot { get; private set; }

    internal bool HoldUsedThisDrop { get; private set; }

    internal int LockDelayTicksRemaining { get; private set; }

    internal int LockResetCount { get; private set; }

    internal bool LockDelayActive { get; private set; }

    internal int GravityTicksRemaining { get; private set; }

    internal int RotationCooldownRemaining { get; private set; }

    internal bool StabilizeArmed { get; private set; }

    internal int StabilizeChargesRemaining { get; private set; }

    internal IReadOnlyList<AnchorTimerSnapshot> StabilizeAnchorTimers => GetStabilizeAnchorSnapshots();

    internal IReadOnlyList<IceTracker.IceTimerSnapshot> IceTimers => _iceTracker.GetTimersSnapshot();

    internal ulong RandomState => _random is IRandomState state
        ? state.State
        : throw new InvalidOperationException("Determinism hash requires a random generator that exposes state.");

    internal ulong WindRandomState => _windRandom is IRandomState state
        ? state.State
        : 0UL;

    /// <summary>
    /// Initializes a new instance of the <see cref="Simulation"/> class.
    /// </summary>
    /// <param name="level">The level definition.</param>
    /// <param name="random">The PRNG to use.</param>
    public Simulation(Level level, IRandom random)
    {
        _level = level ?? throw new ArgumentNullException(nameof(level));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        Grid = new Grid(level.Bounds);
        _movement = new MovementController(Grid, level.Rotation);
        _bag = new PieceBag(level.Bag, _random);
        _allowedGravityDirections = BuildAllowedGravityDirections(level.Rotation);
        (_windHazard, _windRandom) = BuildWindHazard(level);
        StabilizeChargesRemaining = Math.Max(0, level.Abilities?.StabilizeCharges ?? 0);

        // 1. Initial voxels
        foreach (VoxelData voxelData in level.InitialVoxels)
        {
            Grid.SetVoxel(voxelData.Pos, new Voxel(voxelData.Type, voxelData.MaterialId));
            if (voxelData.Type == OccupancyType.Drain)
            {
                _drainConfigs[voxelData.Pos] = voxelData.Drain ?? DrainConfig.Default;
            }
        }

        // 2. Initial spawn
        SpawnNextPiece(resetHoldUsage: true);
    }

    /// <summary>
    /// Advances the simulation by one tick.
    /// Canonical pipeline: Apply Input -> Apply Gravity / Check Lock -> Resolve (if locked) -> Objectives.
    /// </summary>
    /// <param name="command">The player input command for this tick.</param>
    public void Tick(InputCommand command)
    {
        if (_status != SimulationStatus.InProgress)
        {
            return;
        }

        _ticksElapsed++;

        bool wasGrounded = IsGrounded();
        GravityDirection previousGravity = _movement.Gravity;
        ActivePiece? pieceBeforeInput = ActivePiece;

        // 1. Apply Input
        InputApplyResult inputResult = command == InputCommand.Hold
            ? ApplyHold()
            : command == InputCommand.Stabilize
                ? ApplyStabilize()
                : IsWorldRotation(command) && !CanAttemptWorldRotation(command)
                    ? new InputApplyResult(Accepted: false, Moved: false, LockRequested: false)
                    : _movement.ProcessInput(command);

        bool pieceSwapped = pieceBeforeInput != ActivePiece;

        // Immediate Tilt Resolve if world rotation was accepted
        if (inputResult.Accepted && IsWorldRotation(command))
        {
            // Per §3.2: Immediate Tilt Resolve for settled world
            bool tiltAccepted = ResolveTilt();
            if (!tiltAccepted)
            {
                _movement.SetGravity(previousGravity);
                inputResult = new InputApplyResult(Accepted: false, Moved: false, LockRequested: false);
            }
        }

        bool rotationAccepted = inputResult.Accepted && IsWorldRotation(command);
        if (rotationAccepted)
        {
            RotationsExecuted++;
            RotationCooldownRemaining = _level.Rotation.CooldownTicks ?? 0;
            AdvanceStabilizeAnchors();
        }

        // 2. Wind + Gravity Step
        // If hard drop was requested, movement already happened in ProcessInput
        bool lockRequested = inputResult.LockRequested;

        if (pieceSwapped)
        {
            wasGrounded = false;
        }

        ApplyWindHazard(lockRequested);

        bool isGroundedAfterInput = IsGrounded();
        if (wasGrounded && !isGroundedAfterInput && !pieceSwapped)
        {
            HandleUngroundedTransition();
        }

        bool softDropRequested = command == InputCommand.SoftDrop && inputResult.Accepted;

        if (!lockRequested && softDropRequested)
        {
            ResetGravityTimer();
        }

        if (!lockRequested && !softDropRequested && IsGravityTick())
        {
            // Apply natural gravity
            _ = _movement.ApplyGravityStep();
        }

        bool isGroundedAfterGravity = IsGrounded();
        if (!lockRequested)
        {
            lockRequested = UpdateLockDelay(isGroundedAfterGravity);
        }

        // 3. Resolve Phase (only if locked)
        bool resolvedThisTick = false;
        if (lockRequested)
        {
            Resolve();
            _piecesLocked++;
            SpawnNextPiece(resetHoldUsage: true);
            resolvedThisTick = true;
        }

        // 4. Evaluate Objectives & Fail States
        if (resolvedThisTick)
        {
            UpdateStatus();
        }

        if (!rotationAccepted && RotationCooldownRemaining > 0)
        {
            RotationCooldownRemaining--;
        }
    }

    /// <summary>
    /// Computes a deterministic hash of the current simulation state.
    /// </summary>
    public string ComputeDeterminismHash() => DeterminismHasher.Compute(this);

    private void Resolve()
    {
        // Canonical Order (§5):
        // 1. Merge Active Piece
        // 2. Settle Solids
        // 3. Settle Water
        // 4. Recheck Solids
        // 5. Apply Drains
        // 6. Evaluate Objectives (handled in Tick/UpdateStatus)

        List<Int3> displacedWater = [];

        if (ActivePiece != null)
        {
            bool stabilizeAnchors = StabilizeArmed;
            bool reinforcedAnchors = IsReinforcedMaterial(ActivePiece.MaterialId);

            foreach (Int3 pos in ActivePiece.GetWorldPositions())
            {
                Voxel existing = Grid.GetVoxel(pos);
                if (existing.Type == OccupancyType.Water)
                {
                    displacedWater.Add(pos);
                }

                bool anchored = stabilizeAnchors || reinforcedAnchors;
                Grid.SetVoxel(pos, new Voxel(OccupancyType.Solid, ActivePiece.MaterialId, anchored));

                if (stabilizeAnchors && !reinforcedAnchors)
                {
                    _stabilizeAnchorTimers[pos] = Constants.StabilizeAnchorRotations;
                }
            }

            if (stabilizeAnchors)
            {
                StabilizeArmed = false;
            }
        }

        ApplyPendingFreezes();

        SolidSettleResult settleResult = SolidSettler.Settle(Grid, _movement.Gravity);
        displacedWater.AddRange(settleResult.DisplacedWater);

        _ = WaterSolver.Settle(Grid, _movement.Gravity, displacedWater);

        SolidSettleResult recheckResult = SolidSettler.Settle(Grid, _movement.Gravity);
        if (recheckResult.DisplacedWater.Count > 0)
        {
            _ = WaterSolver.Settle(Grid, _movement.Gravity, recheckResult.DisplacedWater);
        }

        ApplyDrains(null);
        ApplyIceTimers(null);
    }

    // Canonical Tilt Resolve (§3.2):
    // Resolve settled world only (no merge of active piece).
    // 1. Settle Solids
    // 2. Settle Water
    // 3. Recheck Solids
    // 4. Apply Drains
    private bool ResolveTilt()
    {
        if (ActivePiece is null)
        {
            SolidSettleResult settleResultNoPiece = SolidSettler.Settle(Grid, _movement.Gravity);
            _ = WaterSolver.Settle(Grid, _movement.Gravity, settleResultNoPiece.DisplacedWater);

            SolidSettleResult recheckResultNoPiece = SolidSettler.Settle(Grid, _movement.Gravity);
            if (recheckResultNoPiece.DisplacedWater.Count > 0)
            {
                _ = WaterSolver.Settle(Grid, _movement.Gravity, recheckResultNoPiece.DisplacedWater);
            }

            ApplyDrains(null);
            ApplyIceTimers(null);
            return true;
        }

        HashSet<Int3> blockedCells = [];
        foreach (Int3 pos in ActivePiece.GetWorldPositions())
        {
            if (Grid.IsInBounds(pos))
            {
                blockedCells.Add(pos);
            }
        }

        Grid snapshot = Grid.Clone();
        bool settled = SolidSettler.TrySettle(Grid, _movement.Gravity, blockedCells, out SolidSettleResult settleResult);
        if (!settled)
        {
            Grid.CopyFrom(snapshot);
            return false;
        }

        _ = WaterSolver.Settle(Grid, _movement.Gravity, settleResult.DisplacedWater, blockedCells);

        bool recheckSettled = SolidSettler.TrySettle(Grid, _movement.Gravity, blockedCells, out SolidSettleResult recheckResult);
        if (!recheckSettled)
        {
            Grid.CopyFrom(snapshot);
            return false;
        }

        if (recheckResult.DisplacedWater.Count > 0)
        {
            _ = WaterSolver.Settle(Grid, _movement.Gravity, recheckResult.DisplacedWater, blockedCells);
        }

        ApplyDrains(blockedCells);
        ApplyIceTimers(blockedCells);
        return true;
    }

    private void SpawnNextPiece(bool resetHoldUsage)
    {
        BagEntry next = _bag.DrawNext();
        SpawnPiece(next, resetHoldUsage);
    }

    private void SpawnPiece(BagEntry entry, bool resetHoldUsage)
    {
        if (resetHoldUsage)
        {
            HoldUsedThisDrop = false;
        }

        ResetDropState();

        PieceDefinition def = PieceLibrary.Get(entry.PieceId);
        OrientedPiece oriented = new(entry.PieceId, def.UniqueOrientations[0], 0);

        // Spawn at top-middle
        Int3 spawnPos = new(_level.Bounds.X / 2, _level.Bounds.Y - 1, _level.Bounds.Z / 2);

        // Reset movement controller with new piece
        _movement.CurrentPiece = new ActivePiece(oriented, spawnPos, entry.MaterialId);

        // check immediate collision (overflow)
        if (!IsPlacementValid(spawnPos, oriented.Voxels))
        {
            _status = SimulationStatus.Lost;
        }
    }

    private bool IsPlacementValid(Int3 origin, IReadOnlyList<Int3> voxels)
    {
        foreach (Int3 voxel in voxels)
        {
            Int3 worldPos = origin + voxel;
            if (!Grid.IsInBounds(worldPos))
            {
                return false;
            }

            Voxel cell = Grid.GetVoxel(worldPos);
            if (cell.Type is not (OccupancyType.Empty or OccupancyType.Water))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateStatus()
    {
        if (_status is SimulationStatus.Lost or SimulationStatus.Won)
        {
            return;
        }

        Objectives = ObjectiveEvaluator.Evaluate(
            Grid,
            _level,
            _piecesLocked,
            WaterRemovedTotal,
            RotationsExecuted);

        if (CheckConstraints())
        {
            _status = SimulationStatus.Lost;
            return;
        }

        if (Objectives.AllCompleted)
        {
            _status = SimulationStatus.Won;
        }
    }

    private void ApplyWindHazard(bool lockRequested)
    {
        if (lockRequested || _windHazard is null)
        {
            return;
        }

        ActivePiece? activePiece = ActivePiece;
        if (activePiece is null)
        {
            return;
        }

        long tickIndex = _ticksElapsed - 1;
        if (tickIndex < _windHazard.OffsetTicks)
        {
            return;
        }

        long sinceOffset = tickIndex - _windHazard.OffsetTicks;
        if (sinceOffset % _windHazard.IntervalTicks != 0)
        {
            return;
        }

        long gustIndex = sinceOffset / _windHazard.IntervalTicks;
        Int3 direction = GetWindDirection(_windHazard, gustIndex);
        int pushSteps = GetWindPushSteps(_windHazard.PushStrength, activePiece.MaterialId);

        for (int i = 0; i < pushSteps; i++)
        {
            if (!activePiece.TryTranslate(direction, Grid))
            {
                break;
            }
        }
    }

    private static bool IsWorldRotation(InputCommand command) =>
        command is InputCommand.RotateWorldForward or
                   InputCommand.RotateWorldBack or
                   InputCommand.RotateWorldLeft or
                   InputCommand.RotateWorldRight;

    private bool CanAttemptWorldRotation(InputCommand command)
    {
        RotationConfig rotation = _level.Rotation;
        if (rotation.CooldownTicks is > 0 && RotationCooldownRemaining > 0)
        {
            return false;
        }

        if (rotation.MaxRotations is not null && RotationsExecuted >= rotation.MaxRotations.Value)
        {
            return false;
        }

        if (rotation.TiltBudget is not null && RotationsExecuted >= rotation.TiltBudget.Value)
        {
            return false;
        }

        if (!TryGetWorldRotationDirection(command, out WorldRotationDirection direction))
        {
            return true;
        }

        GravityDirection? nextGravity = GetRotatedGravity(direction);
        if (nextGravity is null)
        {
            return false;
        }

        return _allowedGravityDirections is null || _allowedGravityDirections.Contains(nextGravity.Value);
    }

    private GravityDirection? GetRotatedGravity(WorldRotationDirection direction)
    {
        Matrix3x3 matrix = GravityTable.GetMatrix(direction);
        return GravityTable.GetRotatedGravity(_movement.Gravity, matrix);
    }

    private static bool TryGetWorldRotationDirection(InputCommand command, out WorldRotationDirection direction)
    {
        if (command == InputCommand.RotateWorldForward)
        {
            direction = WorldRotationDirection.TiltForward;
            return true;
        }

        if (command == InputCommand.RotateWorldBack)
        {
            direction = WorldRotationDirection.TiltBack;
            return true;
        }

        if (command == InputCommand.RotateWorldLeft)
        {
            direction = WorldRotationDirection.TiltLeft;
            return true;
        }

        if (command == InputCommand.RotateWorldRight)
        {
            direction = WorldRotationDirection.TiltRight;
            return true;
        }

        direction = default;
        return false;
    }

    private static HashSet<GravityDirection>? BuildAllowedGravityDirections(RotationConfig rotation)
    {
        if (rotation.AllowedDirections is null)
        {
            return null;
        }

        HashSet<GravityDirection> allowed = [];
        foreach (string direction in rotation.AllowedDirections)
        {
            if (TryParseGravityDirection(direction, out GravityDirection parsed))
            {
                allowed.Add(parsed);
            }
        }

        return allowed;
    }

    private static bool TryParseGravityDirection(string? value, out GravityDirection direction)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            direction = default;
            return false;
        }

        string normalized = value.Trim().ToUpperInvariant();
        direction = normalized switch
        {
            "DOWN" => GravityDirection.Down,
            "NORTH" => GravityDirection.North,
            "SOUTH" => GravityDirection.South,
            "EAST" => GravityDirection.East,
            "WEST" => GravityDirection.West,
            _ => default
        };

        return normalized is "DOWN" or "NORTH" or "SOUTH" or "EAST" or "WEST";
    }

    private static (WindHazard? Hazard, IRandom? Random) BuildWindHazard(Level level)
    {
        if (level.Hazards is null || level.Hazards.Count == 0)
        {
            return (null, null);
        }

        foreach (HazardConfig hazard in level.Hazards)
        {
            if (!hazard.Enabled)
            {
                continue;
            }

            if (!IsWindHazardType(hazard.Type))
            {
                continue;
            }

            if (hazard.Params is null)
            {
                throw new ArgumentException("Wind hazard params are missing.");
            }

            Dictionary<string, object> parameters = hazard.Params;
            int intervalTicks = GetRequiredInt(parameters, "intervalTicks");
            if (intervalTicks < 1)
            {
                intervalTicks = 1;
            }

            int pushStrength = GetRequiredInt(parameters, "pushStrength");
            if (pushStrength < 0)
            {
                pushStrength = 0;
            }

            string modeRaw = GetRequiredString(parameters, "directionMode");
            WindDirectionMode directionMode = ParseWindDirectionMode(modeRaw);

            Int3 fixedDirection = default;
            if (directionMode == WindDirectionMode.Fixed)
            {
                string fixedRaw = GetRequiredString(parameters, "fixedDirection");
                fixedDirection = ParseWindDirection(fixedRaw);
            }

            IRandom? windRandom = null;
            int offsetTicks;
            if (TryGetInt(parameters, "firstGustOffsetTicks", out int offset))
            {
                offsetTicks = Math.Max(0, offset);
            }
            else
            {
                windRandom = CreateWindRandom(level.Meta.Seed);
                offsetTicks = windRandom.NextInt(intervalTicks);
            }

            if (directionMode == WindDirectionMode.RandomSeeded && windRandom is null)
            {
                windRandom = CreateWindRandom(level.Meta.Seed);
            }

            WindHazard windHazard = new(intervalTicks, offsetTicks, pushStrength, directionMode, fixedDirection);
            return (windHazard, windRandom);
        }

        return (null, null);
    }

    private static bool IsWindHazardType(string? type) => NormalizeToken(type) == "WINDGUST";

    private static WindDirectionMode ParseWindDirectionMode(string mode)
    {
        string normalized = NormalizeToken(mode);
        return normalized switch
        {
            "ALTERNATEEW" => WindDirectionMode.AlternateEw,
            "FIXED" => WindDirectionMode.Fixed,
            "RANDOMSEEDED" => WindDirectionMode.RandomSeeded,
            _ => throw new ArgumentException($"Wind direction mode '{mode}' is invalid.")
        };
    }

    private static Int3 ParseWindDirection(string direction)
    {
        string normalized = NormalizeToken(direction);
        return normalized switch
        {
            "EAST" => WindDirectionEast,
            "WEST" => WindDirectionWest,
            "NORTH" => WindDirectionNorth,
            "SOUTH" => WindDirectionSouth,
            _ => throw new ArgumentException($"Wind fixedDirection '{direction}' is invalid.")
        };
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[value.Length];
        int index = 0;
        foreach (char c in value)
        {
            if (c is '_' or '-' or ' ')
            {
                continue;
            }

            buffer[index] = char.ToUpperInvariant(c);
            index++;
        }

        return new string(buffer[..index]);
    }

    private static int GetRequiredInt(Dictionary<string, object> parameters, string key)
    {
        if (TryGetInt(parameters, key, out int value))
        {
            return value;
        }

        throw new ArgumentException($"Hazard parameter '{key}' is missing.");
    }

    private static string GetRequiredString(Dictionary<string, object> parameters, string key)
    {
        if (TryGetString(parameters, key, out string value))
        {
            return value;
        }

        throw new ArgumentException($"Hazard parameter '{key}' is missing.");
    }

    private static bool TryGetInt(Dictionary<string, object> parameters, string key, out int value)
    {
        if (!parameters.TryGetValue(key, out object? raw) || raw is null)
        {
            value = 0;
            return false;
        }

        value = raw switch
        {
            int intValue => intValue,
            long longValue => checked((int)longValue),
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetInt32(),
            JsonElement element when element.ValueKind == JsonValueKind.String => ParseStringInt(element.GetString(), key),
            _ => throw new ArgumentException($"Hazard parameter '{key}' must be an integer.")
        };

        return true;
    }

    private static bool TryGetString(Dictionary<string, object> parameters, string key, out string value)
    {
        if (!parameters.TryGetValue(key, out object? raw) || raw is null)
        {
            value = string.Empty;
            return false;
        }

        value = raw switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            _ => throw new ArgumentException($"Hazard parameter '{key}' must be a string.")
        };

        return true;
    }

    private static int ParseStringInt(string? text, string key)
    {
        if (int.TryParse(text, out int value))
        {
            return value;
        }

        throw new ArgumentException($"Hazard parameter '{key}' must be an integer.");
    }

    private static IRandom CreateWindRandom(uint seed) =>
        new Pcg32(DeriveWindSeed(seed));

    private static ulong DeriveWindSeed(uint seed) =>
        seed ^ WindSeedSalt;

    private Int3 GetWindDirection(WindHazard hazard, long gustIndex) =>
        hazard.DirectionMode switch
        {
            WindDirectionMode.AlternateEw => gustIndex % 2 == 0 ? WindDirectionEast : WindDirectionWest,
            WindDirectionMode.Fixed => hazard.FixedDirection,
            WindDirectionMode.RandomSeeded => GetRandomWindDirection(),
            _ => WindDirectionEast
        };

    private Int3 GetRandomWindDirection()
    {
        if (_windRandom is null)
        {
            throw new InvalidOperationException("Wind hazard requires a random stream.");
        }

        int roll = _windRandom.NextInt(2);
        return roll == 0 ? WindDirectionEast : WindDirectionWest;
    }

    private static int GetWindPushSteps(int pushStrength, string? materialId)
    {
        if (pushStrength <= 0)
        {
            return 0;
        }

        int massFactor = Math.Max(1, GetMaterialMass(materialId));
        return pushStrength / massFactor;
    }

    private bool IsGravityTick()
    {
        if (GravityTicksRemaining > 0)
        {
            GravityTicksRemaining--;
        }

        if (GravityTicksRemaining > 0)
        {
            return false;
        }

        ResetGravityTimer();
        return true;
    }

    private InputApplyResult ApplyHold()
    {
        if (!(_level.Abilities?.HoldEnabled ?? false))
        {
            return new InputApplyResult(Accepted: true, Moved: false, LockRequested: false);
        }

        if (ActivePiece is null)
        {
            return new InputApplyResult(Accepted: false, Moved: false, LockRequested: false);
        }

        if (HoldUsedThisDrop)
        {
            return new InputApplyResult(Accepted: true, Moved: false, LockRequested: false);
        }

        BagEntry current = new(ActivePiece.Piece.Id, ActivePiece.MaterialId);
        if (HoldSlot is null)
        {
            HoldSlot = current;
            SpawnNextPiece(resetHoldUsage: false);
        }
        else
        {
            BagEntry held = HoldSlot.Value;
            HoldSlot = current;
            SpawnPiece(held, resetHoldUsage: false);
        }

        HoldUsedThisDrop = true;
        return new InputApplyResult(Accepted: true, Moved: true, LockRequested: false);
    }

    private InputApplyResult ApplyStabilize()
    {
        if (ActivePiece is null)
        {
            return new InputApplyResult(Accepted: false, Moved: false, LockRequested: false);
        }

        if (StabilizeChargesRemaining <= 0 && !StabilizeArmed)
        {
            return new InputApplyResult(Accepted: true, Moved: false, LockRequested: false);
        }

        if (!StabilizeArmed)
        {
            StabilizeChargesRemaining--;
            StabilizeArmed = true;
            return new InputApplyResult(Accepted: true, Moved: false, LockRequested: false);
        }

        StabilizeArmed = false;
        return new InputApplyResult(Accepted: true, Moved: false, LockRequested: false);
    }

    /// <summary>
    /// Queues a freeze action to be applied during the next resolve.
    /// </summary>
    /// <param name="targets">Target water cells to freeze.</param>
    /// <param name="durationResolves">Duration in resolves.</param>
    public void QueueFreeze(IReadOnlyList<Int3> targets, int durationResolves)
    {
        if (targets is null)
        {
            throw new ArgumentNullException(nameof(targets));
        }

        if (durationResolves <= 0)
        {
            return;
        }

        _pendingFreezes.Add(new FreezeRequest(targets, durationResolves));
    }

    private void ApplyPendingFreezes()
    {
        if (_pendingFreezes.Count == 0)
        {
            return;
        }

        foreach (FreezeRequest request in _pendingFreezes)
        {
            _iceTracker.ApplyFreeze(Grid, request.Targets, request.DurationResolves);
        }

        _pendingFreezes.Clear();
    }

    private void ApplyDrains(ISet<Int3>? blockedCells)
    {
        int removed = DrainSolver.Apply(Grid, _movement.Gravity, _drainConfigs);
        if (removed <= 0)
        {
            return;
        }

        WaterRemovedTotal += removed;
        _ = WaterSolver.Settle(Grid, _movement.Gravity, [], blockedCells);
    }

    private void ApplyIceTimers(ISet<Int3>? blockedCells)
    {
        IReadOnlyList<Int3> thawed = _iceTracker.AdvanceResolve(Grid);
        if (thawed.Count == 0)
        {
            return;
        }

        _ = WaterSolver.Settle(Grid, _movement.Gravity, thawed, blockedCells);
    }

    private bool CheckConstraints()
    {
        ConstraintsConfig? constraints = _level.Constraints;
        if (constraints is null)
        {
            return false;
        }

        if (constraints.MaxMass is int maxMass && GetTotalMass(Grid) > maxMass)
        {
            return true;
        }

        if (constraints.WaterForbiddenWorldHeightMin is int minHeight && HasWaterAtOrAboveWorldHeight(minHeight, Grid))
        {
            return true;
        }

        if (constraints.NoRestingOnWater && HasRestingOnWater(Grid, _movement.Gravity))
        {
            return true;
        }

        return false;
    }

    private static bool HasWaterAtOrAboveWorldHeight(int minHeight, Grid grid)
    {
        int sizeX = grid.Size.X;
        int sizeY = grid.Size.Y;
        int sizeZ = grid.Size.Z;

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = minHeight; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (grid.GetVoxel(new Int3(x, y, z)).Type == OccupancyType.Water)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HasRestingOnWater(Grid grid, GravityDirection gravity)
    {
        Int3 gravityVector = GravityTable.GetVector(gravity);
        int sizeX = grid.Size.X;
        int sizeY = grid.Size.Y;
        int sizeZ = grid.Size.Z;

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    Int3 pos = new(x, y, z);
                    Voxel voxel = grid.GetVoxel(pos);
                    if (!IsSolidForConstraints(voxel.Type))
                    {
                        continue;
                    }

                    Int3 below = pos + gravityVector;
                    if (!grid.TryGetVoxel(below, out Voxel support))
                    {
                        continue;
                    }

                    if (support.Type == OccupancyType.Water)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static int GetTotalMass(Grid grid)
    {
        int total = 0;
        int sizeX = grid.Size.X;
        int sizeY = grid.Size.Y;
        int sizeZ = grid.Size.Z;

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    Voxel voxel = grid.GetVoxel(new Int3(x, y, z));
                    if (voxel.Type != OccupancyType.Solid)
                    {
                        continue;
                    }

                    total += GetMaterialMass(voxel.MaterialId);
                }
            }
        }

        return total;
    }

    private static int GetMaterialMass(string? materialId)
    {
        if (string.IsNullOrWhiteSpace(materialId))
        {
            return 1;
        }

        string normalized = materialId.Trim().ToUpperInvariant();
        return normalized switch
        {
            "HEAVY" => 2,
            "STANDARD" => 1,
            "REINFORCED" => 1,
            _ => 1
        };
    }

    private static bool IsSolidForConstraints(OccupancyType type) =>
        type is OccupancyType.Solid or OccupancyType.Wall or OccupancyType.Bedrock or OccupancyType.Ice
            or OccupancyType.Drain or OccupancyType.Porous;

    private bool IsGrounded() =>
        ActivePiece is not null && !ActivePiece.CanAdvance(Grid, _movement.Gravity);

    private void ResetDropState()
    {
        LockDelayTicksRemaining = Constants.LockDelayTicks;
        LockResetCount = 0;
        LockDelayActive = false;
        StabilizeArmed = false;
        ResetGravityTimer();
    }

    private void ResetGravityTimer() =>
        GravityTicksRemaining = Constants.GravityTicksPerStep;

    private void HandleUngroundedTransition()
    {
        if (LockResetCount < Constants.MaxLockResets)
        {
            LockResetCount++;
            LockDelayTicksRemaining = Constants.LockDelayTicks;
        }

        LockDelayActive = false;
    }

    private bool UpdateLockDelay(bool isGrounded)
    {
        if (!isGrounded)
        {
            LockDelayActive = false;
            return false;
        }

        if (!LockDelayActive)
        {
            LockDelayActive = true;
            if (LockDelayTicksRemaining <= 0)
            {
                LockDelayTicksRemaining = Constants.LockDelayTicks;
            }
        }

        if (LockDelayTicksRemaining > 0)
        {
            LockDelayTicksRemaining--;
        }

        return LockDelayTicksRemaining <= 0;
    }

    private void AdvanceStabilizeAnchors()
    {
        if (_stabilizeAnchorTimers.Count == 0)
        {
            return;
        }

        List<Int3> expired = [];
        List<Int3> keys = [.. _stabilizeAnchorTimers.Keys];
        foreach (Int3 pos in keys)
        {
            int remaining = _stabilizeAnchorTimers[pos] - 1;
            if (remaining <= 0)
            {
                expired.Add(pos);
                continue;
            }

            _stabilizeAnchorTimers[pos] = remaining;
        }

        foreach (Int3 pos in expired)
        {
            _stabilizeAnchorTimers.Remove(pos);
            if (!Grid.TryGetVoxel(pos, out Voxel voxel))
            {
                continue;
            }

            if (!voxel.Anchored || IsReinforcedMaterial(voxel.MaterialId))
            {
                continue;
            }

            Grid.SetVoxel(pos, voxel with { Anchored = false });
        }
    }

    private IReadOnlyList<AnchorTimerSnapshot> GetStabilizeAnchorSnapshots()
    {
        if (_stabilizeAnchorTimers.Count == 0)
        {
            return [];
        }

        List<AnchorTimerSnapshot> snapshots = new(_stabilizeAnchorTimers.Count);
        foreach (KeyValuePair<Int3, int> entry in _stabilizeAnchorTimers)
        {
            snapshots.Add(new AnchorTimerSnapshot(entry.Key, entry.Value));
        }

        snapshots.Sort(CompareAnchorSnapshots);
        return snapshots;
    }

    private static int CompareAnchorSnapshots(AnchorTimerSnapshot left, AnchorTimerSnapshot right) =>
        ComparePositions(left.Position, right.Position);

    private static int ComparePositions(Int3 left, Int3 right)
    {
        int comp = left.X.CompareTo(right.X);
        if (comp != 0)
        {
            return comp;
        }

        comp = left.Y.CompareTo(right.Y);
        if (comp != 0)
        {
            return comp;
        }

        return left.Z.CompareTo(right.Z);
    }

    private static bool IsReinforcedMaterial(string? materialId)
    {
        if (string.IsNullOrWhiteSpace(materialId))
        {
            return false;
        }

        return materialId.Trim().Equals("REINFORCED", StringComparison.OrdinalIgnoreCase);
    }

    private enum WindDirectionMode
    {
        AlternateEw,
        Fixed,
        RandomSeeded
    }

    private sealed record WindHazard(
        int IntervalTicks,
        int OffsetTicks,
        int PushStrength,
        WindDirectionMode DirectionMode,
        Int3 FixedDirection);

    internal readonly record struct AnchorTimerSnapshot(Int3 Position, int RemainingRotations);

    private sealed record FreezeRequest(IReadOnlyList<Int3> Targets, int DurationResolves);
}
