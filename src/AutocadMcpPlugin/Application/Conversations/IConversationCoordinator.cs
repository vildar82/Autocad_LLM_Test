using System.Threading;
using System.Threading.Tasks;

namespace AutocadMcpPlugin.Application.Conversations;

/// <summary>
/// Координирует обработку пользовательских сообщений и взаимодействие с MCP/LLM.
/// </summary>
public interface IConversationCoordinator
{
    /// <summary>
    /// Обрабатывает сообщение пользователя и возвращает ответ ассистента.
    /// </summary>
    Task<string> ProcessUserMessageAsync(string message, CancellationToken cancellationToken = default);
}
