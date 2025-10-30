using System.Threading;
using System.Threading.Tasks;

namespace AutocadMcpPlugin.Application.Conversations;

/// <summary>
/// Базовая заглушка координатора диалогов.
/// </summary>
public sealed class ConversationCoordinator : IConversationCoordinator
{
    // TODO: подключить LLM и MCP; пока возвращаем предустановленный ответ.
    public Task<string> ProcessUserMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var reply = $"Пока не подключено к LLM. Вы попросили: \"{message}\".";
        return Task.FromResult(reply);
    }
}
