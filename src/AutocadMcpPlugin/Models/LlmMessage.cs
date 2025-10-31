using System;

namespace AutocadMcpPlugin;

/// <summary>
/// Сообщение для передачи в LLM.
/// </summary>
public sealed class LlmMessage
{
    public LlmMessage(string role, string content)
    {
        Role = role ?? throw new ArgumentNullException(nameof(role));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public string Role { get; }

    public string Content { get; }

    public static LlmMessage CreateSystem(string content) => new("system", content);

    public static LlmMessage CreateUser(string content) => new("user", content);

    public static LlmMessage CreateAssistant(string content) => new("assistant", content);
}