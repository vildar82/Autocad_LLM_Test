using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutocadMcpPlugin;
using AutocadMcpPlugin.Application.Commands;
using AutocadMcpPlugin.Infrastructure.Configuration;

namespace AutocadMcpPlugin.Application.Conversations;

/// <summary>
/// Координирует обработку пользовательских сообщений, обращения к AutoCAD-командам и LLM.
/// </summary>
public sealed class ConversationCoordinator : IConversationCoordinator
{
    private static readonly Regex NumericValueRegex = new(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);
    private static readonly Regex RadiusRegex = new(@"радиус\w*\s*(?<value>-?\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CenterRegex = new(@"цент\w*\s*(?:в\s*)?\(?(?<x>-?\d+(?:[.,]\d+)?)\s*,\s*(?<y>-?\d+(?:[.,]\d+)?)\)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LineStartRegex = new(@"от\s*(?:точк\w*\s*)?\(?(?<x>-?\d+(?:[.,]\d+)?)\s*,\s*(?<y>-?\d+(?:[.,]\d+)?)\)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LineEndRegex = new(@"(до|в)\s*(?:точк\w*\s*)?\(?(?<x>-?\d+(?:[.,]\d+)?)\s*,\s*(?<y>-?\d+(?:[.,]\d+)?)\)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IAutocadCommandExecutor _commandExecutor;
    private readonly ILlmClient _llmClient;
    private readonly OpenAiSettings _settings;
    private readonly List<LlmMessage> _history;

    public ConversationCoordinator(
        IAutocadCommandExecutor commandExecutor,
        ILlmClient llmClient,
        OpenAiSettings settings)
    {
        _commandExecutor = commandExecutor;
        _llmClient = llmClient;
        _settings = settings;
        _history = new List<LlmMessage>
        {
            LlmMessage.CreateSystem("Ты ассистент проектировщика AutoCAD. Если не можешь выполнить команду самостоятельно, дай понятный текстовый ответ.")
        };
    }

    public async Task<string> ProcessUserMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Уточните запрос.";

        _history.Add(LlmMessage.CreateUser(message));

        if (TryParseCircle(message, out var centerX, out var centerY, out var radius))
        {
            var result = await _commandExecutor.DrawCircleAsync(centerX, centerY, radius, cancellationToken);
            _history.Add(LlmMessage.CreateAssistant(result.Message));
            return result.Message;
        }

        if (TryParseLine(message, out var startX, out var startY, out var endX, out var endY))
        {
            var result = await _commandExecutor.DrawLineAsync(startX, startY, endX, endY, cancellationToken);
            _history.Add(LlmMessage.CreateAssistant(result.Message));
            return result.Message;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            const string messageNoKey = "Чтобы обратиться к языковой модели, задайте API-ключ OpenAI.";
            _history.Add(LlmMessage.CreateAssistant(messageNoKey));
            return messageNoKey;
        }

        var trimmedHistory = TrimHistory(_history);
        var request = new LlmChatRequest(trimmedHistory);
        var response = await _llmClient.CreateChatCompletionAsync(request, cancellationToken);

        if (!response.IsSuccess)
        {
            var errorMessage = response.Error ?? "Не удалось получить ответ от LLM.";
            _history.Add(LlmMessage.CreateAssistant(errorMessage));
            return errorMessage;
        }

        _history.Add(LlmMessage.CreateAssistant(response.Content!));
        return response.Content!;
    }

    private static IReadOnlyList<LlmMessage> TrimHistory(List<LlmMessage> history)
    {
        const int maxMessages = 20;
        if (history.Count <= maxMessages)
            return history;

        return history.GetRange(history.Count - maxMessages, maxMessages);
    }

    private static bool TryParseCircle(string text, out double centerX, out double centerY, out double radius)
    {
        centerX = centerY = radius = 0;
        if (!ContainsInvariant(text, "круг"))
            return false;

        var numbers = NumericValueRegex.Matches(text);
        radius = TryExtractNumber(RadiusRegex.Match(text));
        if (radius <= 0)
        {
            if (numbers.Count == 0)
                return false;

            radius = TryParse(numbers[0].Value);
            if (radius <= 0)
                return false;
        }

        var centerMatch = CenterRegex.Match(text);
        if (centerMatch.Success)
        {
            centerX = TryParse(centerMatch.Groups["x"].Value);
            centerY = TryParse(centerMatch.Groups["y"].Value);
            return true;
        }

        if (numbers.Count >= 3)
        {
            centerX = TryParse(numbers[1].Value);
            centerY = TryParse(numbers[2].Value);
            return true;
        }

        return false;
    }

    private static bool TryParseLine(string text, out double startX, out double startY, out double endX, out double endY)
    {
        startX = startY = endX = endY = 0;
        if (!ContainsInvariant(text, "лини") && !ContainsInvariant(text, "отрез"))
            return false;

        var startMatch = LineStartRegex.Match(text);
        var endMatch = LineEndRegex.Match(text);

        if (startMatch.Success && endMatch.Success)
        {
            startX = TryParse(startMatch.Groups["x"].Value);
            startY = TryParse(startMatch.Groups["y"].Value);
            endX = TryParse(endMatch.Groups["x"].Value);
            endY = TryParse(endMatch.Groups["y"].Value);
            return true;
        }

        var numbers = NumericValueRegex.Matches(text);
        if (numbers.Count >= 4)
        {
            startX = TryParse(numbers[0].Value);
            startY = TryParse(numbers[1].Value);
            endX = TryParse(numbers[2].Value);
            endY = TryParse(numbers[3].Value);
            return true;
        }

        return false;
    }

    private static double TryExtractNumber(Match match) =>
        match.Success ? TryParse(match.Groups["value"].Value) : 0;

    private static bool ContainsInvariant(string source, string value) =>
        source.IndexOf(value, StringComparison.InvariantCultureIgnoreCase) >= 0;

    private static double TryParse(string input)
    {
        var normalized = input.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}
