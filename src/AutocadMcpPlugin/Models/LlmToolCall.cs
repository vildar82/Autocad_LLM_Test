using System.Text.Json;

namespace AutocadMcpPlugin;

/// <summary>
/// Инструкция LLM на вызов инструмента.
/// </summary>
public record LlmToolCall(string Id, string Name, JsonElement Arguments);