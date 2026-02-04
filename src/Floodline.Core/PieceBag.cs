using System;
using System.Collections.Generic;
using Floodline.Core.Levels;
using Floodline.Core.Random;

namespace Floodline.Core;

/// <summary>
/// Deterministic piece bag with FIXED_SEQUENCE or WEIGHTED modes.
/// </summary>
public sealed class PieceBag
{
    private static readonly HashSet<string> AllowedMaterials = new(StringComparer.OrdinalIgnoreCase)
    {
        "STANDARD",
        "HEAVY",
        "REINFORCED"
    };

    private readonly BagMode _mode;
    private readonly BagEntry[] _sequence;
    private int _sequenceIndex;
    private readonly WeightedEntry[] _weights;
    private readonly int _totalWeight;
    private readonly IRandom _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="PieceBag"/> class.
    /// </summary>
    /// <param name="config">The bag configuration.</param>
    /// <param name="random">The deterministic RNG.</param>
    public PieceBag(BagConfig config, IRandom random)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (random is null)
        {
            throw new ArgumentNullException(nameof(random));
        }

        _random = random;
        _mode = ParseMode(config.Type);

        if (_mode == BagMode.FixedSequence)
        {
            if (config.Sequence is null || config.Sequence.Length == 0)
            {
                throw new ArgumentException("FIXED_SEQUENCE bag requires a non-empty sequence.");
            }

            _sequence = new BagEntry[config.Sequence.Length];
            for (int i = 0; i < config.Sequence.Length; i++)
            {
                _sequence[i] = ParseToken(config.Sequence[i]);
            }

            _weights = [];
            _totalWeight = 0;
        }
        else
        {
            if (config.Weights is null || config.Weights.Count == 0)
            {
                throw new ArgumentException("WEIGHTED bag requires non-empty weights.");
            }

            List<WeightedEntry> weighted = [];
            List<string> keys = [.. config.Weights.Keys];
            keys.Sort(StringComparer.Ordinal);

            int total = 0;
            foreach (string key in keys)
            {
                int weight = config.Weights[key];
                if (weight < 0)
                {
                    throw new ArgumentException($"Weight for '{key}' must be >= 0.");
                }

                if (weight == 0)
                {
                    continue;
                }

                BagEntry entry = ParseToken(key);
                weighted.Add(new WeightedEntry(entry, weight));
                total = checked(total + weight);
            }

            if (total <= 0)
            {
                throw new ArgumentException("WEIGHTED bag must include at least one positive weight.");
            }

            _sequence = [];
            _weights = [.. weighted];
            _totalWeight = total;
        }
    }

    /// <summary>
    /// Draws the next piece from the bag.
    /// </summary>
    public BagEntry DrawNext()
    {
        if (_mode == BagMode.FixedSequence)
        {
            int index = _sequenceIndex % _sequence.Length;
            _sequenceIndex++;
            return _sequence[index];
        }

        int roll = _random.NextInt(_totalWeight);
        foreach (WeightedEntry entry in _weights)
        {
            if (roll < entry.Weight)
            {
                return entry.Entry;
            }

            roll -= entry.Weight;
        }

        return _weights[^1].Entry;
    }

    /// <summary>
    /// Returns the next N bag entries without advancing the RNG or sequence index.
    /// </summary>
    public IReadOnlyList<BagEntry> PeekNext(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        List<BagEntry> preview = new(count);

        if (_mode == BagMode.FixedSequence)
        {
            int index = _sequenceIndex;
            for (int i = 0; i < count; i++)
            {
                int seqIndex = index % _sequence.Length;
                preview.Add(_sequence[seqIndex]);
                index++;
            }

            return preview;
        }

        if (_random is not IRandomCloneable cloneable)
        {
            throw new InvalidOperationException("WEIGHTED bag preview requires a cloneable RNG.");
        }

        IRandom previewRandom = cloneable.Clone();
        for (int i = 0; i < count; i++)
        {
            preview.Add(DrawWeighted(previewRandom));
        }

        return preview;
    }

    private BagEntry DrawWeighted(IRandom random)
    {
        int roll = random.NextInt(_totalWeight);
        foreach (WeightedEntry entry in _weights)
        {
            if (roll < entry.Weight)
            {
                return entry.Entry;
            }

            roll -= entry.Weight;
        }

        return _weights[^1].Entry;
    }

    private static BagMode ParseMode(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Bag type is missing.");
        }

        string normalized = NormalizeType(type);
        return normalized switch
        {
            "FIXED" => BagMode.FixedSequence,
            "FIXEDSEQUENCE" => BagMode.FixedSequence,
            "WEIGHTED" => BagMode.Weighted,
            _ => throw new ArgumentException($"Bag type '{type}' is not supported.")
        };
    }

    private static BagEntry ParseToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Bag token is empty.");
        }

        string trimmed = token.Trim();
        int separatorIndex = trimmed.IndexOf(':', StringComparison.Ordinal);
        string pieceToken;
        string? materialToken = null;

        if (separatorIndex < 0)
        {
            pieceToken = trimmed;
        }
        else
        {
            if (separatorIndex == 0 || separatorIndex == trimmed.Length - 1 || trimmed.IndexOf(':', separatorIndex + 1) >= 0)
            {
                throw new ArgumentException($"Bag token '{token}' is invalid.");
            }

            pieceToken = trimmed[..separatorIndex];
            materialToken = trimmed[(separatorIndex + 1)..];
        }

        if (!Enum.TryParse(pieceToken, ignoreCase: true, out PieceId pieceId))
        {
            throw new ArgumentException($"Bag token '{token}' has an unknown piece id.");
        }

        string? materialId = null;
        if (!string.IsNullOrWhiteSpace(materialToken))
        {
            string materialNormalized = materialToken.Trim().ToUpperInvariant();
            if (!AllowedMaterials.Contains(materialNormalized))
            {
                throw new ArgumentException($"Bag token '{token}' has an unknown material id.");
            }

            materialId = materialNormalized;
        }

        return new BagEntry(pieceId, materialId);
    }

    private static string NormalizeType(string typeRaw)
    {
        Span<char> buffer = stackalloc char[typeRaw.Length];
        int index = 0;
        foreach (char c in typeRaw)
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

    private readonly record struct WeightedEntry(BagEntry Entry, int Weight);

    private enum BagMode
    {
        FixedSequence,
        Weighted
    }
}

/// <summary>
/// A deterministic bag entry (piece + optional material).
/// </summary>
public readonly record struct BagEntry(PieceId PieceId, string? MaterialId);
