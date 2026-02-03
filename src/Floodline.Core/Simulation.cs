using System.Diagnostics.CodeAnalysis;
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
    private SimulationStatus _status = SimulationStatus.InProgress;
    private long _ticksElapsed;
    private int _piecesLocked;

    /// <summary>
    /// Gets the current grid.
    /// </summary>
    public Grid Grid { get; }

    /// <summary>
    /// Gets the current simulation state.
    /// </summary>
    public SimulationState State => new(_status, _ticksElapsed, _piecesLocked);

    /// <summary>
    /// Gets the current active piece, if any.
    /// </summary>
    public ActivePiece? ActivePiece => _movement.CurrentPiece;

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

        // 1. Initial voxels
        foreach (VoxelData voxelData in level.InitialVoxels)
        {
            Grid.SetVoxel(voxelData.Pos, new Voxel(voxelData.Type, voxelData.MaterialId));
        }

        // 2. Initial spawn
        SpawnNextPiece();
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

        // 1. Apply Input
        InputApplyResult inputResult = _movement.ProcessInput(command);

        // Immediate Tilt Resolve if world rotation was accepted
        if (inputResult.Accepted && IsWorldRotation(command))
        {
            // Per §3.2: Immediate Tilt Resolve for settled world
            ResolveTilt();
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
        if (lockRequested)
        {
            Resolve();
            _piecesLocked++;
            SpawnNextPiece();
        }

        // 4. Evaluate Objectives & Fail States
        UpdateStatus();
    }

    private void Resolve()
    {
        // Canonical Order (§5):
        // 1. Merge Active Piece
        // 2. Settle Solids
        // 3. Settle Water
        // 4. Recheck Solids
        // 5. Apply Drains
        // 6. Evaluate Objectives (handled in Tick/UpdateStatus)

        // TODO (FL-0112): Full water solver implementation
        // TODO (FL-0113): Drains and freeze

        // Current skeleton: just merge the active piece voxels into the grid
        if (ActivePiece != null)
        {
            foreach (Int3 pos in ActivePiece.GetWorldPositions())
            {
                // MaterialId is not in OrientedPiece yet, using default or piece-based if needed.
                // For now, using default MaterialId from PieceDefinition if available.
                Grid.SetVoxel(pos, new Voxel(OccupancyType.Solid, null));
            }
        }

        _ = SolidSettler.Settle(Grid, _movement.Gravity);
    }

    // Canonical Tilt Resolve (§3.2):
    // Resolve settled world only (no merge of active piece).
    // 1. Settle Solids
    // 2. Settle Water
    // 3. Recheck Solids
    // 4. Apply Drains
    //
    // TODO (FL-0112): Full water solver implementation
    // TODO (FL-0113): Drains and freeze
    private void ResolveTilt() => SolidSettler.Settle(Grid, _movement.Gravity);

    private void SpawnNextPiece()
    {
        // TODO (FL-0105/bag logic): For now, pick a piece from the library based on random
        // In M1, we just need a functioning spawning loop.
        PieceId nextId = _random.NextChoice((PieceId[])Enum.GetValues(typeof(PieceId)));
        PieceDefinition def = PieceLibrary.Get(nextId);
        OrientedPiece oriented = new(nextId, def.UniqueOrientations[0], 0);

        // Spawn at top-middle
        Int3 spawnPos = new(_level.Bounds.X / 2, _level.Bounds.Y - 1, _level.Bounds.Z / 2);

        // Reset movement controller with new piece
        _movement.CurrentPiece = new ActivePiece(oriented, spawnPos);

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

    private static void UpdateStatus()
    {
        // TODO: Objective evaluation
    }

    private static bool IsWorldRotation(InputCommand command) =>
        command is InputCommand.RotateWorldForward or
                   InputCommand.RotateWorldBack or
                   InputCommand.RotateWorldLeft or
                   InputCommand.RotateWorldRight;

    private static bool IsGravityTick() => false;
}
