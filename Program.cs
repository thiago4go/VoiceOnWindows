namespace VoiceOnWindows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var context = new DictationApplicationContext();
        Application.Run(context);
    }
}
