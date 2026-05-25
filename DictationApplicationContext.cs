using System.Diagnostics;
using System.Drawing;

namespace VoiceOnWindows;

internal sealed class DictationApplicationContext : ApplicationContext
{
    private readonly ConfigStore _store = new();
    private readonly NotifyIcon _tray;
    private readonly GlobalHotkey _hotkey = new();
    private readonly WhisperClient _whisperClient = new();
    private readonly PasteService _pasteService = new();
    private AppConfig _config;
    private WaveRecorder? _recorder;
    private SettingsForm? _settingsForm;
    private AppState _state = AppState.Idle;
    private string _lastError = "";

    public DictationApplicationContext()
    {
        _config = _store.Load();

        _tray = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Voice On Windows",
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowSettings();

        _hotkey.Pressed += (_, _) => ToggleRecording();
        RegisterHotkey();
        UpdateTrayMenu();
        Notify("Voice On Windows", $"Ready. Hotkey: {_config.Hotkey}");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _recorder?.Dispose();
            _hotkey.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _whisperClient.Dispose();
            _settingsForm?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Icon LoadIcon()
    {
        Icon? embeddedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (embeddedIcon is not null) return embeddedIcon;

        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "assets", "app.ico"),
            Path.Combine(AppContext.BaseDirectory, "app.ico"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "assets", "app.ico")
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate)) return new Icon(candidate);
        }

        return SystemIcons.Application;
    }

    private void RegisterHotkey()
    {
        try
        {
            _hotkey.Register(_config.Hotkey);
            _store.Log("Hotkey registered", new { _config.Hotkey });
        }
        catch (Exception ex)
        {
            SetState(AppState.Error, ex.Message);
            Notify("Voice On Windows error", ex.Message);
            _store.Log("Hotkey registration failed", new { _config.Hotkey, error = ex.Message });
        }
    }

    private void ToggleRecording()
    {
        if (_state == AppState.Recording)
        {
            StopAndTranscribe();
            return;
        }

        if (_state == AppState.Transcribing)
        {
            Notify("Voice On Windows", "Still transcribing the previous recording.");
            return;
        }

        StartRecording();
    }

    private void StartRecording()
    {
        try
        {
            _recorder?.Dispose();
            _recorder = new WaveRecorder();
            _recorder.Start();
            SetState(AppState.Recording);
            Notify("Voice On Windows", "Recording started. Press the hotkey again to stop.");
            _store.Log("Recording started");
        }
        catch (Exception ex)
        {
            SetState(AppState.Error, ex.Message);
            Notify("Voice On Windows error", ex.Message);
            _store.Log("Recording start failed", new { error = ex.ToString() });
        }
    }

    private async void StopAndTranscribe()
    {
        WaveRecorder? recorder = _recorder;
        _recorder = null;

        try
        {
            SetState(AppState.Transcribing);
            byte[] audio = recorder?.StopToWav() ?? [];
            recorder?.Dispose();

            if (audio.Length < 128)
            {
                throw new InvalidOperationException("Recording was empty.");
            }

            if (_config.KeepAudioFiles)
            {
                Directory.CreateDirectory(_store.RecordingsDirectory);
                var audioPath = Path.Combine(_store.RecordingsDirectory, $"dictation-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.wav");
                await File.WriteAllBytesAsync(audioPath, audio);
                _store.Log("Audio file saved", new { audioPath, bytes = audio.Length });
            }

            _store.Log("Transcription request started", new
            {
                _config.Endpoint,
                _config.Model,
                bytes = audio.Length
            });

            string text = await _whisperClient.TranscribeAsync(audio, _config);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Transcription returned no text.");
            }

            Clipboard.SetText(text);
            bool pasted = false;
            if (_config.PasteAfterTranscription)
            {
                try
                {
                    await _pasteService.PasteAsync(_config.PasteDelayMs);
                    pasted = true;
                }
                catch (Exception pasteError)
                {
                    _store.Log("Paste failed after copying transcript", new { error = pasteError.ToString() });
                }
            }

            SetState(AppState.Idle);
            Notify("Voice On Windows", pasted ? "Text pasted." : "Text copied.");
            _store.Log("Dictation complete", new { characters = text.Length });
        }
        catch (Exception ex)
        {
            recorder?.Dispose();
            SetState(AppState.Error, ex.Message);
            Notify("Voice On Windows error", ex.Message);
            _store.Log("Dictation failed", new { error = ex.ToString() });
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Show();
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_config);
        _settingsForm.SettingsSaved += (_, nextConfig) =>
        {
            _config = nextConfig;
            _store.Save(_config);
            RegisterHotkey();
            UpdateTrayMenu();
            _settingsForm.UpdateStatus(GetStatusText());
        };
        _settingsForm.Show();
        _settingsForm.UpdateStatus(GetStatusText());
    }

    private void OpenFile(string path)
    {
        try
        {
            if (!File.Exists(path)) File.WriteAllText(path, "");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Notify("Voice On Windows error", ex.Message);
        }
    }

    private void SetState(AppState state, string error = "")
    {
        _state = state;
        _lastError = error;
        UpdateTrayMenu();
        _settingsForm?.UpdateStatus(GetStatusText());
    }

    private string GetStatusText()
    {
        return _state switch
        {
            AppState.Recording => "Recording",
            AppState.Transcribing => "Transcribing",
            AppState.Error => "Error: " + _lastError,
            _ => "Ready"
        };
    }

    private void UpdateTrayMenu()
    {
        _tray.Text = "Voice On Windows - " + GetStatusText().TruncateForTray();
        _tray.ContextMenuStrip = new ContextMenuStrip();
        _tray.ContextMenuStrip.Items.Add($"Status: {GetStatusText()}").Enabled = false;
        _tray.ContextMenuStrip.Items.Add($"Hotkey: {_config.Hotkey}").Enabled = false;
        _tray.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _tray.ContextMenuStrip.Items.Add(
            _state == AppState.Recording ? "Stop recording" : "Start recording",
            null,
            (_, _) => ToggleRecording()).Enabled = _state != AppState.Transcribing;
        _tray.ContextMenuStrip.Items.Add("Settings", null, (_, _) => ShowSettings());
        _tray.ContextMenuStrip.Items.Add("Open config", null, (_, _) => OpenFile(_store.ConfigPath));
        _tray.ContextMenuStrip.Items.Add("Open logs", null, (_, _) => OpenFile(_store.LogPath));
        _tray.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        _tray.ContextMenuStrip.Items.Add("Reload config", null, (_, _) =>
        {
            _config = _store.Load();
            RegisterHotkey();
            UpdateTrayMenu();
            Notify("Voice On Windows", "Configuration reloaded.");
        });
        _tray.ContextMenuStrip.Items.Add("Quit", null, (_, _) => ExitThread());
    }

    private void Notify(string title, string message)
    {
        if (!_config.ShowNotifications) return;
        _tray.BalloonTipTitle = title;
        _tray.BalloonTipText = message;
        _tray.ShowBalloonTip(2500);
    }

    private enum AppState
    {
        Idle,
        Recording,
        Transcribing,
        Error
    }
}

internal static class TrayTextExtensions
{
    public static string TruncateForTray(this string value)
    {
        return value.Length <= 48 ? value : value[..45] + "...";
    }
}
