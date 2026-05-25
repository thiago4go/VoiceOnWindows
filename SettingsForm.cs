namespace VoiceOnWindows;

internal sealed class SettingsForm : Form
{
    private readonly TextBox _endpoint = new();
    private readonly TextBox _model = new();
    private readonly ComboBox _requestFormat = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _hotkey = new();
    private readonly TextBox _apiKey = new() { UseSystemPasswordChar = true };
    private readonly TextBox _openRouterReferer = new();
    private readonly TextBox _openRouterTitle = new();
    private readonly NumericUpDown _pasteDelayMs = new() { Minimum = 0, Maximum = 2000 };
    private readonly NumericUpDown _requestTimeoutMs = new() { Minimum = 5000, Maximum = 600000, Increment = 5000 };
    private readonly CheckBox _pasteAfterTranscription = new() { Text = "Paste after transcription" };
    private readonly CheckBox _showNotifications = new() { Text = "Show notifications" };
    private readonly CheckBox _keepAudioFiles = new() { Text = "Keep audio files for debugging" };
    private readonly Label _status = new() { AutoSize = true };
    private AppConfig _config;

    public event EventHandler<AppConfig>? SettingsSaved;

    public SettingsForm(AppConfig config)
    {
        _config = config;
        Text = "Voice On Windows Settings";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 520);
        Size = new Size(620, 600);
        Padding = new Padding(18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            AutoScroll = true
        };
        Controls.Add(layout);

        var title = new Label
        {
            Text = "Voice On Windows",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        };
        layout.Controls.Add(title);
        layout.Controls.Add(_status);
        layout.Controls.Add(Field("Endpoint", _endpoint));
        layout.Controls.Add(Field("Model", _model));
        _requestFormat.Items.AddRange(new object[] { "Auto", "MultipartForm", "OpenRouterJson" });
        layout.Controls.Add(Field("Request format", _requestFormat));
        layout.Controls.Add(Field("Hotkey", _hotkey));
        layout.Controls.Add(Field("API key", _apiKey));
        layout.Controls.Add(Field("OpenRouter referer", _openRouterReferer));
        layout.Controls.Add(Field("OpenRouter title", _openRouterTitle));
        layout.Controls.Add(Field("Paste delay (ms)", _pasteDelayMs));
        layout.Controls.Add(Field("Request timeout (ms)", _requestTimeoutMs));
        layout.Controls.Add(_pasteAfterTranscription);
        layout.Controls.Add(_showNotifications);
        layout.Controls.Add(_keepAudioFiles);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 12, 0, 0)
        };

        var save = new Button { Text = "Save", AutoSize = true };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        layout.Controls.Add(buttons);

        LoadValues(config);
    }

    public void UpdateStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateStatus(status));
            return;
        }

        _status.Text = "Status: " + status;
    }

    private static Control Field(string label, Control input)
    {
        input.Dock = DockStyle.Top;

        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            Padding = new Padding(0, 8, 0, 0)
        };
        panel.Controls.Add(input);
        panel.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Dock = DockStyle.Top
        });
        return panel;
    }

    private void LoadValues(AppConfig config)
    {
        _endpoint.Text = config.Endpoint;
        _model.Text = config.Model;
        _requestFormat.SelectedItem = config.RequestFormat;
        if (_requestFormat.SelectedIndex < 0) _requestFormat.SelectedItem = "Auto";
        _hotkey.Text = config.Hotkey;
        _apiKey.Text = config.ApiKey;
        _openRouterReferer.Text = config.OpenRouterReferer;
        _openRouterTitle.Text = config.OpenRouterTitle;
        _pasteDelayMs.Value = config.PasteDelayMs;
        _requestTimeoutMs.Value = config.RequestTimeoutMs;
        _pasteAfterTranscription.Checked = config.PasteAfterTranscription;
        _showNotifications.Checked = config.ShowNotifications;
        _keepAudioFiles.Checked = config.KeepAudioFiles;
    }

    private void Save()
    {
        _config = new AppConfig
        {
            Endpoint = _endpoint.Text,
            Model = _model.Text,
            RequestFormat = _requestFormat.SelectedItem?.ToString() ?? "Auto",
            Hotkey = _hotkey.Text,
            ApiKey = _apiKey.Text,
            OpenRouterReferer = _openRouterReferer.Text,
            OpenRouterTitle = _openRouterTitle.Text,
            PasteDelayMs = (int)_pasteDelayMs.Value,
            RequestTimeoutMs = (int)_requestTimeoutMs.Value,
            PasteAfterTranscription = _pasteAfterTranscription.Checked,
            ShowNotifications = _showNotifications.Checked,
            KeepAudioFiles = _keepAudioFiles.Checked,
            ConfigPath = _config.ConfigPath,
            LogPath = _config.LogPath
        };
        _config.Normalize();
        LoadValues(_config);
        SettingsSaved?.Invoke(this, _config);
    }
}
