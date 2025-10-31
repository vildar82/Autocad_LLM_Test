using System.Collections.Generic;

namespace AutocadLlmPlugin;

/// <summary>
/// Запрос к LLM на генерацию ответа.
/// </summary>
public sealed class LlmChatRequest(
    IReadOnlyList<LlmMessage> messages,
    IReadOnlyList<LlmToolDefinition>? tools = null,
    string? model = null,
    double? temperature = null)
{
    public IReadOnlyList<LlmMessage> Messages { get; } = messages;

    public IReadOnlyList<LlmToolDefinition> Tools { get; } = tools ?? [];

    public string? Model { get; } = model;

    public double? Temperature { get; } = temperature;
}