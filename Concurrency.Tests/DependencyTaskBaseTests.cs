namespace Concurrency.Tests;

#if DEBUG

using NLog;
using NLog.Config;
using NLog.Targets;

public sealed class DependencyTaskBaseTests
{
  private sealed class TestActor : SerializableObject
  {
    protected override ValueTask DisposeAsyncCore()
    {
      return ValueTask.CompletedTask;
    }
  }

  [Fact]
  public void ChainDepth_ExceedsThreshold_LogsWarning()
  {
    var originalConfig = LogManager.Configuration;
    var memTarget = new MemoryTarget("depthWarningTest");
    var testConfig = new LoggingConfiguration();
    testConfig.AddRule(LogLevel.Warn, LogLevel.Fatal, memTarget, "Systems.*");
    LogManager.Configuration = testConfig;
    LogManager.ReconfigExistingLoggers();

    try
    {
      var actor = new TestActor();
      var gate = new ManualResetEventSlim();
      const int chainSize = 600;
      var completed = new CountdownEvent(chainSize + 1);

      actor.Post(
        () =>
        {
          gate.Wait();
          completed.Signal();
        },
        dispatchToThreadPool: true);

      for (var i = 0; i < chainSize; i++)
      {
        actor.Post(() => completed.Signal());
      }

      gate.Set();
      Assert.True(completed.Wait(TimeSpan.FromSeconds(30)));
      Assert.Contains(memTarget.Logs, static l => l.Contains("500"));
    }
    finally
    {
      LogManager.Configuration = originalConfig;
      LogManager.ReconfigExistingLoggers();
    }
  }
}

#endif
