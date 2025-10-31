using System.Threading;
using System.Threading.Tasks;

namespace AutocadLlmPlugin;

/// <summary>
/// Координирует обработку пользовательских сообщений и вызовы инструментов.
/// </summary>
public interface IConversationCoordinator
{
    Task<string> ProcessUserMessageAsync(string message, CancellationToken cancellationToken = default);
}