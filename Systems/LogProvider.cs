namespace Systems;

using NLog;

public static class LogProvider
{
  public static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
}
