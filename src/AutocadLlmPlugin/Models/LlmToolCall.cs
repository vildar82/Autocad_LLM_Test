using System.Text.Json;

namespace AutocadLlmPlugin;

/// <summary>
/// Инструкция LLM на вызов инструмента.
/// </summary>
public record LlmToolCall(string Id, string Name, JsonElement Arguments);