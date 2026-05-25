using System.Runtime.InteropServices;

namespace VoiceOnWindows;

internal sealed class PasteService
{
    private static readonly ushort[] ModifierKeys =
    [
        VkLControl,
        VkRControl,
        VkLShift,
        VkRShift,
        VkLMenu,
        VkRMenu,
        VkLWin,
        VkRWin
    ];

    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;
    private const ushort VkLControl = 0xA2;
    private const ushort VkRControl = 0xA3;
    private const ushort VkLShift = 0xA0;
    private const ushort VkRShift = 0xA1;
    private const ushort VkLMenu = 0xA4;
    private const ushort VkRMenu = 0xA5;
    private const ushort VkLWin = 0x5B;
    private const ushort VkRWin = 0x5C;
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;

    public async Task PasteAsync(int delayMs)
    {
        await Task.Delay(Math.Clamp(delayMs, 0, 2000));

        try
        {
            List<ushort> released = ReleaseHeldModifiers();

            try
            {
                SendKeyChord([VkControl, VkV]);
                return;
            }
            finally
            {
                RestoreModifiers(released);
            }
        }
        catch
        {
            await PasteWithShellAsync();
        }
    }

    private static List<ushort> ReleaseHeldModifiers()
    {
        var released = new List<ushort>();
        var inputs = new List<Input>();

        foreach (ushort key in ModifierKeys)
        {
            if ((GetAsyncKeyState(key) & 0x8000) == 0) continue;
            released.Add(key);
            inputs.Add(CreateKeyboardInput(key, KeyUp));
        }

        SendInputs(inputs);
        return released;
    }

    private static void RestoreModifiers(List<ushort> released)
    {
        var inputs = released.Select(key => CreateKeyboardInput(key, 0)).ToList();
        SendInputs(inputs);
    }

    private static void SendKeyChord(IReadOnlyList<ushort> keys)
    {
        var inputs = new List<Input>();

        foreach (ushort key in keys)
        {
            inputs.Add(CreateKeyboardInput(key, 0));
        }

        for (int i = keys.Count - 1; i >= 0; i--)
        {
            inputs.Add(CreateKeyboardInput(keys[i], KeyUp));
        }

        SendInputs(inputs);
    }

    private static void SendInputs(List<Input> inputs)
    {
        if (inputs.Count == 0) return;

        uint sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
        if (sent != inputs.Count)
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Windows SendInput failed while pasting. Sent {sent}/{inputs.Count}, error {error}.");
        }
    }

    private static Task PasteWithShellAsync()
    {
        SendKeys.SendWait("^v");
        return Task.CompletedTask;
    }

    private static Input CreateKeyboardInput(ushort key, uint flags)
    {
        return new Input
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput
            {
                VirtualKey = key,
                ScanCode = (ushort)MapVirtualKey(key, 0),
                Flags = flags
            }
        };
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
