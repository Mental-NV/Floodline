using System;
using System.Collections.Generic;
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
    private readonly Level _level;
    private readonly IRandom _random;
    private readonly MovementController _movement;
    private readonly PieceBag _bag;
    private readonly Dictionary<Int3, DrainConfig> _drainConfigs = [];
    private readonly IceTracker _iceTracker = new();
    private readonly List<FreezeRequest> _pendingFreezes = [];
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

    internal IReadOnlyList<IceTracker.IceTimerSnapshot> IceTimers => _iceTracker.GetTimersSnapshot();

    internal ulong RandomState => _random is IRandomState state
        ? state.State
        : throw new InvalidOperationException("Determinism hash requires a random generator that exposes state.");

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

        GravityDirection previousGravity = _movement.Gravity;

        // 1. Apply Input
        InputApplyResult inputResult = command == InputCommand.Hold
            ? ApplyHold()
            : _movement.ProcessInput(command);

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

        if (inputResult.Accepted && IsWorldRotation(command))
        {
            RotationsExecuted++;
        }

        // 2. Gravity Step
        // If hard drop was requested, movement already happened in ProcessInput
        bool lockRequested = inputResult.LockRequested;

        if (!lockRequested && IsGravityTick())
        {
            // Apply natural gravity or soft drop (which is just extra gravity ticks)
            bool gravityMoved = _movement.ApplyGravityStep();
            if (!gravityMoved)
            {
                lockRequested = true;
            }
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
            foreach (Int3 pos in ActivePiece.GetWorldPositions())
            {
                Voxel existing = Grid.GetVoxel(pos);
                if (existing.Type == OccupancyType.Water)
                {
                    displacedWater.Add(pos);
                }

                Grid.SetVoxel(pos, new Voxel(OccupancyType.Solid, ActivePiece.MaterialId));
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

        if (Objectives.AllCompleted)
        {
            _status = SimulationStatus.Won;
        }
    }

    private static bool IsWorldRotation(InputCommand command) =>
        command is InputCommand.RotateWorldForward or
                   InputCommand.RotateWorldBack or
                   InputCommand.RotateWorldLeft or
                   InputCommand.RotateWorldRight;

    private static bool IsGravityTick() => false;

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

    private sealed record FreezeRequest(IReadOnlyList<Int3> Targets, int DurationResolves);
}
