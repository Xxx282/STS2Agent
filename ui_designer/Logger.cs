namespace STS2Agent.Designer;

public static class Logger
{
    public static void Info(string message) => GD.Print($"[STS2Agent] {message}");
    public static void Warn(string message) => GD.PushWarning($"[STS2Agent] {message}");
    public static void Error(string message) => GD.PushError($"[STS2Agent] {message}");
}
