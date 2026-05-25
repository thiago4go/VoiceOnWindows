using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VoiceOnWindows;

internal sealed class WhisperClient : IDisposable
{
    private readonly HttpClient _httpClient = new();

    public async Task<string> TranscribeAsync(byte[] wavBytes, AppConfig config)
    {
        return ShouldUseOpenRouterJson(config)
            ? await TranscribeOpenRouterJsonAsync(wavBytes, config)
            : await TranscribeMultipartAsync(wavBytes, config);
    }

    private async Task<string> TranscribeMultipartAsync(byte[] wavBytes, AppConfig config)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint);
        using var form = new MultipartFormDataContent();
        using var audioContent = new ByteArrayContent(wavBytes);
        using var timeout = new CancellationTokenSource(config.RequestTimeoutMs);

        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audioContent, "file", "dictation.wav");
        form.Add(new StringContent(config.Model), "model");
        request.Content = form;

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        return await SendAndReadTextAsync(request, timeout.Token);
    }

    private async Task<string> TranscribeOpenRouterJsonAsync(byte[] wavBytes, AppConfig config)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint);
        using var timeout = new CancellationTokenSource(config.RequestTimeoutMs);
        var payload = new
        {
            model = config.Model,
            input_audio = new
            {
                data = Convert.ToBase64String(wavBytes),
                format = "wav"
            }
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        if (!string.IsNullOrWhiteSpace(config.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(config.OpenRouterReferer))
        {
            request.Headers.TryAddWithoutValidation("HTTP-Referer", config.OpenRouterReferer);
        }

        if (!string.IsNullOrWhiteSpace(config.OpenRouterTitle))
        {
            request.Headers.TryAddWithoutValidation("X-OpenRouter-Title", config.OpenRouterTitle);
        }

        return await SendAndReadTextAsync(request, timeout.Token);
    }

    private async Task<string> SendAndReadTextAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ASR {(int)response.StatusCode}: {body}");
        }

        using JsonDocument document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("text", out JsonElement textElement))
        {
            throw new InvalidOperationException("ASR response did not include a text field.");
        }

        return textElement.GetString()?.Trim() ?? "";
    }

    private static bool ShouldUseOpenRouterJson(AppConfig config)
    {
        if (config.RequestFormat.Equals("OpenRouterJson", StringComparison.OrdinalIgnoreCase)) return true;
        if (config.RequestFormat.Equals("MultipartForm", StringComparison.OrdinalIgnoreCase)) return false;

        return Uri.TryCreate(config.Endpoint, UriKind.Absolute, out Uri? uri) &&
               uri.Host.Equals("openrouter.ai", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
