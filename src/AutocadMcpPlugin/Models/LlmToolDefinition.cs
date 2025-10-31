using System.Text.Json;

namespace AutocadMcpPlugin;

/// <summary>
/// Описание инструмента для LLM.
/// </summary>
public record LlmToolDefinition(string Name, string Description, JsonElement Parameters);