using System.Threading;
using System.Threading.Tasks;

namespace AutocadMcpPlugin;

/// <summary>
/// Клиент для работы с LLM.
/// </summary>
public interface ILlmClient
{
    Task<LlmChatResult> CreateChatCompletionAsync(LlmChatRequest request, CancellationToken cancellationToken = default);
}