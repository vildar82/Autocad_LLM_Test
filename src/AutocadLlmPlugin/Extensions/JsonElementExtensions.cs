using System;
using System.Globalization;
using System.Text.Json;

namespace AutocadLlmPlugin;

public static class JsonElementExtensions
{
    public static double GetDouble(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            throw new InvalidOperationException($"Не найден параметр '{propertyName}'.");

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => double.Parse(value.GetString() ?? string.Empty, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Некорректный формат параметра '{propertyName}'.")
        };
    }

    public static string GetString(this JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            throw new InvalidOperationException($"Не найден параметр '{propertyName}'.");

        return value.ValueKind switch
        {
            JsonValueKind.String => (value.GetString() ?? string.Empty).Trim(),
            _ => throw new InvalidOperationException($"Некорректный формат параметра '{propertyName}'. Ожидалась строка.")
        };
    }

    public static bool ReadOptionalBoolean(this JsonElement element, string propertyName, bool defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return defaultValue;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) ? parsed : throw new InvalidOperationException($"Некорректный формат параметра '{propertyName}'."),
            JsonValueKind.Null or JsonValueKind.Undefined => defaultValue,
            _ => throw new InvalidOperationException($"Некорректный формат параметра '{propertyName}'.")
        };
    }
}