using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Floodline.Core.Levels;

/// <summary>
/// Computes a canonical hash of level JSON content.
/// </summary>
public static class LevelHash
{
    public const string HashVersion = "0.1.0";

    public static string Compute(string levelJson)
    {
        if (levelJson is null)
        {
            throw new ArgumentNullException(nameof(levelJson));
        }

        using JsonDocument doc = JsonDocument.Parse(levelJson);
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false });

        WriteCanonical(doc.RootElement, writer);
        writer.Flush();

        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream.ToArray());
        return $"{HashVersion}:{ToHexLower(hash)}";
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(element, writer);
                return;
            case JsonValueKind.Array:
                WriteArray(element, writer);
                return;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                return;
            case JsonValueKind.Number:
                WriteNumber(element, writer);
                return;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                return;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                return;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                return;
            case JsonValueKind.Undefined:
            default:
                throw new ArgumentException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }

    private static void WriteObject(JsonElement element, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();

        List<JsonProperty> props = [];
        foreach (JsonProperty property in element.EnumerateObject())
        {
            props.Add(property);
        }

        props.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
        foreach (JsonProperty property in props)
        {
            writer.WritePropertyName(property.Name);
            WriteCanonical(property.Value, writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteArray(JsonElement element, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        foreach (JsonElement item in element.EnumerateArray())
        {
            WriteCanonical(item, writer);
        }

        writer.WriteEndArray();
    }

    private static void WriteNumber(JsonElement element, Utf8JsonWriter writer)
    {
        if (!element.TryGetInt64(out long value))
        {
            throw new ArgumentException("Level JSON contains a non-integer numeric value.");
        }

        writer.WriteNumberValue(value);
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
