using System.Text.Json;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.DeepResearch.Abstractions;

namespace DeepResearchSample;

/// <summary>
/// IronHive IMessageGenerator를 DeepResearch ITextGenerationService로 어댑트
/// </summary>
public class IronHiveTextGenerationAdapter : ITextGenerationService
{
    private readonly IMessageGenerator _generator;
    private readonly string _modelId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public IronHiveTextGenerationAdapter(IMessageGenerator generator, string modelId = "gpt-4o-mini")
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _modelId = modelId;
    }

    /// <inheritdoc />
    public async Task<TextGenerationResult> GenerateAsync(
        string prompt,
        TextGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(prompt, options);
        var response = await _generator.GenerateMessageAsync(request, cancellationToken);

        var text = ExtractTextFromResponse(response);

        return new TextGenerationResult
        {
            Text = text,
            TokenUsage = response.TokenUsage != null
                ? new TokenUsageInfo
                {
                    PromptTokens = response.TokenUsage.InputTokens,
                    CompletionTokens = response.TokenUsage.OutputTokens
                }
                : null,
            FinishReason = response.DoneReason?.ToString()
        };
    }

    /// <inheritdoc />
    public async Task<T?> GenerateStructuredAsync<T>(
        string prompt,
        TextGenerationOptions? options = null,
        CancellationToken cancellationToken = default) where T : class
    {
        // JSON 응답 요청
        var jsonPrompt = $"{prompt}\n\n응답은 반드시 유효한 JSON 형식으로만 제공해주세요. 마크다운 코드 블록이나 다른 텍스트 없이 순수 JSON만 반환해주세요.";

        var result = await GenerateAsync(jsonPrompt, options, cancellationToken);

        try
        {
            var json = ExtractJsonFromText(result.Text);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private MessageGenerationRequest CreateRequest(string prompt, TextGenerationOptions? options)
    {
        var messages = new List<Message>
        {
            new UserMessage
            {
                Content = [new TextMessageContent { Value = prompt }]
            }
        };

        return new MessageGenerationRequest
        {
            Model = _modelId,
            System = options?.SystemPrompt,
            Messages = messages,
            Temperature = (float?)(options?.Temperature) ?? 0.7f,
            MaxTokens = options?.MaxTokens ?? 2048
        };
    }

    private static string ExtractTextFromResponse(MessageResponse response)
    {
        var textContents = response.Message.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }

    private static string ExtractJsonFromText(string text)
    {
        var jsonMatch = System.Text.RegularExpressions.Regex.Match(
            text, @"```(?:json)?\s*([\s\S]*?)\s*```");

        if (jsonMatch.Success)
        {
            return jsonMatch.Groups[1].Value.Trim();
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return trimmed;
        }

        var objectMatch = System.Text.RegularExpressions.Regex.Match(text, @"\{[\s\S]*\}");
        if (objectMatch.Success)
        {
            return objectMatch.Value;
        }

        var arrayMatch = System.Text.RegularExpressions.Regex.Match(text, @"\[[\s\S]*\]");
        if (arrayMatch.Success)
        {
            return arrayMatch.Value;
        }

        return text;
    }
}
