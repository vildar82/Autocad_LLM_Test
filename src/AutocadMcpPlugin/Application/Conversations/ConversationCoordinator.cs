using System.Threading;
using System.Threading.Tasks;

namespace AutocadMcpPlugin.Application.Conversations;

/// <summary>
/// Базовая заглушка, координирующая диалог до подключения LLM/MCP.
/// </summary>
public sealed class ConversationCoordinator : IConversationCoordinator
{
    // TODO: заменить на вызовы LLM и MCP, как только будут готовы интеграции.
    public Task<string> ProcessUserMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var reply = $"Пока нет связи с LLM. Вы запросили: \"{message}\".";
        return Task.FromResult(reply);
    }
}
