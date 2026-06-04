namespace Systems;

public static class UnhandledExceptionHandler
{
  public static event Action<Exception>? UnhandledException;

  public static bool OnExceptionOccured(Exception e)
  {
    LogProvider.Logger.Error(e, e.Message);
    UnhandledException?.Invoke(e);

    return true;
  }
}
