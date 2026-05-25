using System.Net.Http.Headers;
using System.Text.Json;

namespace VoiceOnWindows;

internal sealed class WhisperClient : IDisposable
{
    private readonly HttpClient _httpClient = new();

    public async Task<string> TranscribeAsync(byte[] wavBytes, AppConfig config)
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

        using var response = await _httpClient.SendAsync(request, timeout.Token);
        string body = await response.Content.ReadAsStringAsync(timeout.Token);

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

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
