using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AutocadMcpPlugin.Application.Commands;

namespace AutocadMcpPlugin.Application.Conversations;

/// <summary>
/// Координирует обработку пользовательских сообщений и запуск команд AutoCAD.
/// </summary>
public sealed class ConversationCoordinator : IConversationCoordinator
{
    private static readonly Regex NumericValueRegex = new(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);
    private static readonly Regex RadiusRegex = new(@"радиус\w*\s*(?<value>-?\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CenterRegex = new(@"цент\w*\s*(?:в\s*)?\(?(?<x>-?\d+(?:[.,]\d+)?)\s*,\s*(?<y>-?\d+(?:[.,]\d+)?)\)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LineStartRegex = new(@"от\s*(?:точк\w*\s*)?\(?(?<x>-?\d+(?:[.,]\d+)?)\s*,\s*(?<y>-?\d+(?:[.,]\d+)?)\)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LineEndRegex = new(@"(до|в)\s*(?:точк\w*\s*)?\(?(?<x>-?\d+(?:[.,]\d+)?)\s*,\s*(?<y>-?\d+(?:[.,]\d+)?)\)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IAutocadCommandExecutor _commandExecutor;

    public ConversationCoordinator(IAutocadCommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
    }

    public async Task<string> ProcessUserMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Уточните запрос.";

        if (TryParseCircle(message, out var centerX, out var centerY, out var radius))
        {
            var result = await _commandExecutor.DrawCircleAsync(centerX, centerY, radius, cancellationToken);
            return result.Message;
        }

        if (TryParseLine(message, out var startX, out var startY, out var endX, out var endY))
        {
            var result = await _commandExecutor.DrawLineAsync(startX, startY, endX, endY, cancellationToken);
            return result.Message;
        }

        return "Пока понимаю команды: круг (радиус + центр) и линия (от точки до точки). Попробуйте сформулировать запрос точнее.";
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