using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VoiceOnWindows;

internal sealed class GlobalHotkey : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly LowLevelKeyboardProc _callback;
    private IntPtr _hook;
    private HotkeyDefinition _definition = HotkeyDefinition.Parse("Ctrl+Space");
    private bool _isPressed;

    public event EventHandler? Pressed;

    public GlobalHotkey()
    {
        _callback = HookCallback;
    }

    public void Register(string accelerator)
    {
        _definition = HotkeyDefinition.Parse(accelerator);

        if (_hook != IntPtr.Zero)
        {
            return;
        }

        using Process currentProcess = Process.GetCurrentProcess();
        using ProcessModule? currentModule = currentProcess.MainModule;
        IntPtr moduleHandle = currentModule is null ? IntPtr.Zero : GetModuleHandle(currentModule.ModuleName);
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, moduleHandle, 0);

        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not install the Windows keyboard hook.");
        }
    }

    public void Unregister()
    {
        if (_hook == IntPtr.Zero) return;

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _isPressed = false;
    }

    public void Dispose()
    {
        Unregister();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        int message = wParam.ToInt32();
        bool isDown = message is WmKeyDown or WmSysKeyDown;
        bool isUp = message is WmKeyUp or WmSysKeyUp;
        if (!isDown && !isUp)
        {
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<KeyboardHookStruct>(lParam);
        uint vk = data.VirtualKeyCode;
        bool targetEvent = vk == _definition.VirtualKey;
        bool modifiersSatisfied = _definition.AreModifiersPressed(vk, isDown, isUp);

        if (targetEvent && modifiersSatisfied)
        {
            if (isDown && !_isPressed)
            {
                _isPressed = true;
                Pressed?.Invoke(this, EventArgs.Empty);
            }
            else if (isUp)
            {
                _isPressed = false;
            }

            return new IntPtr(1);
        }

        if (_isPressed && isUp && targetEvent)
        {
            _isPressed = false;
            return new IntPtr(1);
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private sealed record HotkeyDefinition(
        bool Ctrl,
        bool Alt,
        bool Shift,
        bool Windows,
        uint VirtualKey)
    {
        public static HotkeyDefinition Parse(string accelerator)
        {
            bool ctrl = false;
            bool alt = false;
            bool shift = false;
            bool windows = false;
            uint key = 0;

            foreach (string rawPart in accelerator.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string part = rawPart.Replace("CommandOrControl", "Ctrl", StringComparison.OrdinalIgnoreCase);
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl = true;
                    continue;
                }

                if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Option", StringComparison.OrdinalIgnoreCase))
                {
                    alt = true;
                    continue;
                }

                if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    shift = true;
                    continue;
                }

                if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Super", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Meta", StringComparison.OrdinalIgnoreCase))
                {
                    windows = true;
                    continue;
                }

                key = ParseKey(part);
            }

            if (key == 0)
            {
                throw new FormatException("Hotkey must include a non-modifier key, for example Ctrl+Space.");
            }

            return new HotkeyDefinition(ctrl, alt, shift, windows, key);
        }

        public bool AreModifiersPressed(uint eventVirtualKey, bool isDown, bool isUp)
        {
            return (!Ctrl || IsPressed(Keys.ControlKey, eventVirtualKey, isDown, isUp)) &&
                   (!Alt || IsPressed(Keys.Menu, eventVirtualKey, isDown, isUp)) &&
                   (!Shift || IsPressed(Keys.ShiftKey, eventVirtualKey, isDown, isUp)) &&
                   (!Windows || IsPressed(Keys.LWin, eventVirtualKey, isDown, isUp) ||
                    IsPressed(Keys.RWin, eventVirtualKey, isDown, isUp));
        }

        private static bool IsPressed(Keys key, uint eventVirtualKey, bool isDown, bool isUp)
        {
            uint vk = (uint)key;
            if (eventVirtualKey == vk)
            {
                return isDown || !isUp;
            }

            return (GetAsyncKeyState((int)vk) & 0x8000) != 0;
        }

        private static uint ParseKey(string key)
        {
            if (key.Length == 1)
            {
                char c = char.ToUpperInvariant(key[0]);
                if (c is >= 'A' and <= 'Z') return c;
                if (c is >= '0' and <= '9') return c;
            }

            return key.ToLowerInvariant() switch
            {
                "space" => (uint)Keys.Space,
                "tab" => (uint)Keys.Tab,
                "escape" or "esc" => (uint)Keys.Escape,
                "enter" or "return" => (uint)Keys.Enter,
                "insert" => (uint)Keys.Insert,
                "home" => (uint)Keys.Home,
                "end" => (uint)Keys.End,
                "pageup" => (uint)Keys.PageUp,
                "pagedown" => (uint)Keys.PageDown,
                "pause" => (uint)Keys.Pause,
                "scrolllock" => (uint)Keys.Scroll,
                "capslock" => (uint)Keys.CapsLock,
                _ when Enum.TryParse(key, ignoreCase: true, out Keys parsed) => (uint)parsed,
                _ => throw new FormatException($"Unsupported hotkey key '{key}'.")
            };
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookStruct
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
