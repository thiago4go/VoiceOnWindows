# Voice On Windows

Voice On Windows is a tiny native Windows tray app for push-to-talk dictation.

It records from your microphone, sends the audio to an OpenAI-compatible
transcription endpoint, copies the returned text to the clipboard, and pastes it
into the active input.

This project is intentionally separate from OpenWhispr. It was inspired by the
same general dictation workflow, but this codebase is a fresh Windows-native
implementation in C#/.NET.

## What it does

- Runs quietly in the Windows tray
- Uses `Ctrl+Space` as the default start/stop hotkey
- Records WAV audio from the default microphone
- Sends audio to a configurable ASR endpoint
- Supports multipart OpenAI-style transcription APIs and OpenRouter's base64 JSON STT API
- Copies the transcript to the clipboard
- Attempts to paste into the active field
- Keeps working even if auto-paste fails, because the text remains copied

## Default ASR configuration

```text
Endpoint: http://100.106.120.20:8001/v1/audio/transcriptions
Model: whisper-1
```

The request matches OpenAI-style audio transcription APIs:

```text
file=@dictation.wav
model=whisper-1
```

## Build

Install the .NET 8 SDK, then run:

```powershell
.\build.ps1
```

The published EXE is written to:

```text
bin\Release\net8.0-windows\win-x64\publish\VoiceOnWindows.exe
```

## Configure

Open the tray menu and choose **Settings**, or edit:

```text
%APPDATA%\Voice On Windows\config.json
```

Example:

```json
{
  "Endpoint": "http://100.106.120.20:8001/v1/audio/transcriptions",
  "Model": "whisper-1",
  "RequestFormat": "Auto",
  "Hotkey": "Ctrl+Space",
  "PasteAfterTranscription": true,
  "PasteDelayMs": 120,
  "RequestTimeoutMs": 120000,
  "KeepAudioFiles": false,
  "ShowNotifications": true,
  "ApiKey": "",
  "OpenRouterReferer": "",
  "OpenRouterTitle": "Voice On Windows"
}
```

Hotkeys use Windows-style names such as `Ctrl+Space`, `Ctrl+Shift+D`, or
`Alt+Space`.

### OpenRouter

For OpenRouter STT, use:

```json
{
  "Endpoint": "https://openrouter.ai/api/v1/audio/transcriptions",
  "Model": "qwen/qwen3-asr-flash-2026-02-10",
  "RequestFormat": "Auto",
  "ApiKey": "YOUR_OPENROUTER_API_KEY"
}
```

`Auto` uses OpenRouter's JSON request format when the endpoint host is
`openrouter.ai`:

```json
{
  "model": "qwen/qwen3-asr-flash-2026-02-10",
  "input_audio": {
    "data": "base64-wav-audio",
    "format": "wav"
  }
}
```

For a non-OpenRouter service that also expects this shape, set:

```json
"RequestFormat": "OpenRouterJson"
```

For classic OpenAI-compatible multipart uploads, set:

```json
"RequestFormat": "MultipartForm"
```

## Notes

This app does not perform local transcription. It depends on the configured ASR
endpoint being reachable from the Windows machine.

The current endpoint response is expected to include a top-level `text` field.
