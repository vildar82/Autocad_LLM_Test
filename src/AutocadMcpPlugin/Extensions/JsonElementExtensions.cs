using System;
using System.Globalization;
using System.Text.Json;

namespace AutocadMcpPlugin;

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
}