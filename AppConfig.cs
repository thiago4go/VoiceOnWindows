using System.Text.Json.Serialization;

namespace VoiceOnWindows;

internal sealed class AppConfig
{
    public string Endpoint { get; set; } = "http://100.106.120.20:8001/v1/audio/transcriptions";
    public string Model { get; set; } = "whisper-1";
    public string RequestFormat { get; set; } = "Auto";
    public string Hotkey { get; set; } = "Ctrl+Space";
    public bool PasteAfterTranscription { get; set; } = true;
    public int PasteDelayMs { get; set; } = 120;
    public int RequestTimeoutMs { get; set; } = 120000;
    public bool KeepAudioFiles { get; set; }
    public bool ShowNotifications { get; set; } = true;
    public string ApiKey { get; set; } = "";
    public string OpenRouterReferer { get; set; } = "";
    public string OpenRouterTitle { get; set; } = "Voice On Windows";

    [JsonIgnore]
    public string ConfigPath { get; set; } = "";

    [JsonIgnore]
    public string LogPath { get; set; } = "";

    public void Normalize()
    {
        Endpoint = string.IsNullOrWhiteSpace(Endpoint)
            ? "http://100.106.120.20:8001/v1/audio/transcriptions"
            : Endpoint.Trim();
        Model = string.IsNullOrWhiteSpace(Model) ? "whisper-1" : Model.Trim();
        RequestFormat = NormalizeRequestFormat(RequestFormat);
        Hotkey = string.IsNullOrWhiteSpace(Hotkey) ? "Ctrl+Space" : Hotkey.Trim();
        ApiKey = ApiKey.Trim();
        OpenRouterReferer = OpenRouterReferer.Trim();
        OpenRouterTitle = string.IsNullOrWhiteSpace(OpenRouterTitle) ? "Voice On Windows" : OpenRouterTitle.Trim();
        PasteDelayMs = Math.Clamp(PasteDelayMs, 0, 2000);
        RequestTimeoutMs = Math.Clamp(RequestTimeoutMs, 5000, 600000);
    }

    private static string NormalizeRequestFormat(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "multipart" or "multipartform" or "multipart_form" or "multipart-form" => "MultipartForm",
            "openrouter" or "openrouterjson" or "openrouter_json" or "jsonbase64" or "json_base64" => "OpenRouterJson",
            _ => "Auto"
        };
    }
}
