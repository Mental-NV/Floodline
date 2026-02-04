using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Floodline.Core;
using Floodline.Core.Movement;

namespace Floodline.Core.Determinism;

/// <summary>
/// Computes a deterministic hash over the simulation state.
/// </summary>
public static class DeterminismHasher
{
    public const string HashVersion = "0.1.6";

    public static string Compute(Simulation simulation)
    {
        if (simulation is null)
        {
            throw new ArgumentNullException(nameof(simulation));
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        HashWriter writer = new(hash);

        writer.WriteString(HashVersion);
        WriteState(writer, simulation);

        byte[] digest = hash.GetHashAndReset();
        string hex = ToHexLower(digest);
        return $"{HashVersion}:{hex}";
    }

    private static void WriteState(HashWriter writer, Simulation simulation)
    {
        SimulationState state = simulation.State;
        writer.WriteInt32((int)state.Status);
        writer.WriteInt64(state.TicksElapsed);
        writer.WriteInt32(state.PiecesLocked);
        writer.WriteInt32(simulation.WaterRemovedTotal);
        writer.WriteInt32(simulation.RotationsExecuted);
        writer.WriteInt32(simulation.RotationCooldownRemaining);
        writer.WriteInt32((int)simulation.Gravity);
        writer.WriteUInt64(simulation.RandomState);
        writer.WriteUInt64(simulation.WindRandomState);

        Grid grid = simulation.Grid;
        writer.WriteInt32(grid.Size.X);
        writer.WriteInt32(grid.Size.Y);
        writer.WriteInt32(grid.Size.Z);

        for (int x = 0; x < grid.Size.X; x++)
        {
            for (int y = 0; y < grid.Size.Y; y++)
            {
                for (int z = 0; z < grid.Size.Z; z++)
                {
                    Voxel cell = grid.GetVoxel(new Int3(x, y, z));
                    writer.WriteInt32((int)cell.Type);
                    writer.WriteString(cell.MaterialId);
                    writer.WriteBool(cell.Anchored);
                }
            }
        }

        IReadOnlyList<IceTracker.IceTimerSnapshot> timers = simulation.IceTimers;
        writer.WriteInt32(timers.Count);
        foreach (IceTracker.IceTimerSnapshot timer in timers)
        {
            writer.WriteInt32(timer.Position.X);
            writer.WriteInt32(timer.Position.Y);
            writer.WriteInt32(timer.Position.Z);
            writer.WriteInt32(timer.Remaining);
        }

        ActivePiece? activePiece = simulation.ActivePiece;
        writer.WriteBool(activePiece is not null);
        if (activePiece is not null)
        {
            writer.WriteInt32((int)activePiece.Piece.Id);
            writer.WriteInt32(activePiece.Piece.OrientationIndex);
            writer.WriteInt32(activePiece.Origin.X);
            writer.WriteInt32(activePiece.Origin.Y);
            writer.WriteInt32(activePiece.Origin.Z);
            writer.WriteString(activePiece.MaterialId);
        }

        writer.WriteBool(simulation.HoldUsedThisDrop);
        BagEntry? holdSlot = simulation.HoldSlot;
        writer.WriteBool(holdSlot.HasValue);
        if (holdSlot.HasValue)
        {
            BagEntry slot = holdSlot.Value;
            writer.WriteInt32((int)slot.PieceId);
            writer.WriteString(slot.MaterialId);
        }

        writer.WriteBool(simulation.StabilizeArmed);
        writer.WriteInt32(simulation.StabilizeChargesRemaining);
        IReadOnlyList<Simulation.AnchorTimerSnapshot> stabilizeAnchors = simulation.StabilizeAnchorTimers;
        writer.WriteInt32(stabilizeAnchors.Count);
        foreach (Simulation.AnchorTimerSnapshot anchor in stabilizeAnchors)
        {
            writer.WriteInt32(anchor.Position.X);
            writer.WriteInt32(anchor.Position.Y);
            writer.WriteInt32(anchor.Position.Z);
            writer.WriteInt32(anchor.RemainingRotations);
        }

        writer.WriteInt32(simulation.LockDelayTicksRemaining);
        writer.WriteInt32(simulation.LockResetCount);
        writer.WriteBool(simulation.LockDelayActive);
        writer.WriteInt32(simulation.GravityTicksRemaining);

        ObjectiveEvaluation objectives = simulation.Objectives;
        writer.WriteInt32(objectives.Objectives.Count);
        writer.WriteBool(objectives.AllCompleted);
        foreach (ObjectiveProgress progress in objectives.Objectives)
        {
            writer.WriteString(progress.Type);
            writer.WriteInt32(progress.Current);
            writer.WriteInt32(progress.Target);
            writer.WriteBool(progress.Completed);
        }
    }

    private sealed class HashWriter(IncrementalHash hash)
    {
        private readonly IncrementalHash _hash = hash;
        private readonly byte[] _buffer1 = new byte[1];
        private readonly byte[] _buffer4 = new byte[4];
        private readonly byte[] _buffer8 = new byte[8];

        public void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(_buffer4, value);
            _hash.AppendData(_buffer4);
        }

        public void WriteInt64(long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(_buffer8, value);
            _hash.AppendData(_buffer8);
        }

        public void WriteUInt64(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(_buffer8, value);
            _hash.AppendData(_buffer8);
        }

        public void WriteBool(bool value)
        {
            _buffer1[0] = value ? (byte)1 : (byte)0;
            _hash.AppendData(_buffer1);
        }

        public void WriteString(string? value)
        {
            if (value is null)
            {
                WriteInt32(-1);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteInt32(bytes.Length);
            _hash.AppendData(bytes);
        }
    }

    private static string ToHexLower(byte[] data)
    {
        char[] chars = new char[data.Length * 2];
        const string hex = "0123456789abcdef";

        for (int i = 0; i < data.Length; i++)
        {
            byte value = data[i];
            int index = i * 2;
            chars[index] = hex[value >> 4];
            chars[index + 1] = hex[value & 0xF];
        }

        return new string(chars);
    }
}
