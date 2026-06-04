namespace Concurrency.Tests;

using Systems;

public sealed class SerializableObjectTests
{
  private sealed class TestActor : SerializableObject
  {
    protected override ValueTask DisposeAsyncCore()
    {
      return ValueTask.CompletedTask;
    }
  }

  private sealed class ThrowingDisposeActor : SerializableObject
  {
    protected override ValueTask DisposeAsyncCore()
    {
      throw new InvalidOperationException("dispose throw");
    }
  }

  [Fact]
  public void Post_Sync_ExecutesAction()
  {
    var actor = new TestActor();
    var executed = false;
    actor.Post(() => executed = true);
    Assert.True(executed);
  }

  [Fact]
  public void Post_Sync_MultipleSequentialCalls_AllExecute()
  {
    var actor = new TestActor();
    var count = 0;
    actor.Post(() => count++);
    actor.Post(() => count++);
    actor.Post(() => count++);
    Assert.Equal(3, count);
  }

  [Fact]
  public void Post_Sync_ExceptionInAction_DoesNotThrow()
  {
    var actor = new TestActor();
    actor.Post(() => throw new InvalidOperationException("test"));
  }

  [Fact]
  public void Post_Async_ExecutesFunc()
  {
    var actor = new TestActor();
    var executed = false;
    var completed = new ManualResetEventSlim();
    actor.Post(async () =>
    {
      await Task.Yield();
      executed = true;
      completed.Set();
    });
    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    Assert.True(executed);
  }

  [Fact]
  public void Post_Async_ExceptionInFunc_DoesNotThrow()
  {
    var actor = new TestActor();
    var completed = new ManualResetEventSlim();
    actor.Post(async () =>
    {
      await Task.Yield();
      completed.Set();
      throw new InvalidOperationException("test");
    });
    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
  }

  [Fact]
  public void Post_Async_SyncThrowBeforeAwait_DoesNotThrow()
  {
    var actor = new TestActor();
    Func<Task> func = () => throw new InvalidOperationException("sync throw");
    actor.Post(func);
  }

  [Fact]
  public void Post_Sync_DispatchToThreadPool_RunsOnPoolThread()
  {
    var actor = new TestActor();
    var ranOnPool = false;
    var completed = new ManualResetEventSlim();
    actor.Post(
      () =>
      {
        ranOnPool = Thread.CurrentThread.IsThreadPoolThread;
        completed.Set();
      },
      dispatchToThreadPool: true);
    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    Assert.True(ranOnPool);
  }

  [Fact]
  public void PostWith_Sync_ExecutesAction()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var executed = false;
    actor1.PostWith(() => executed = true, actor2);
    Assert.True(executed);
  }

  [Fact]
  public void PostWith_Sync_ThreeActors_ExecutesAction()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var actor3 = new TestActor();
    var executed = false;
    actor1.PostWith(() => executed = true, actor2, actor3);
    Assert.True(executed);
  }

  [Fact]
  public void PostWith_Sync_DepsInReverseOrder_ExecutesAction()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var executed = false;
    actor2.PostWith(() => executed = true, actor1);
    Assert.True(executed);
  }

#if DEBUG
  [Fact]
  public void PostWith_Sync_DuplicateActor_ThrowsArgumentException()
  {
    var actor = new TestActor();
    Assert.Throws<ArgumentException>(() => actor.PostWith(() => { }, actor));
  }
#endif

  [Fact]
  public void PostWith_Async_ExecutesFunc()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var executed = false;
    var completed = new ManualResetEventSlim();
    actor1.PostWith(async () =>
    {
      await Task.Yield();
      executed = true;
      completed.Set();
    }, actor2);
    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    Assert.True(executed);
  }

  [Fact]
  public void PostWith_Async_ThreeActors_ExecutesFunc()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var actor3 = new TestActor();
    var executed = false;
    var completed = new ManualResetEventSlim();
    actor1.PostWith(async () =>
    {
      await Task.Yield();
      executed = true;
      completed.Set();
    }, actor2, actor3);
    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    Assert.True(executed);
  }

#if DEBUG
  [Fact]
  public void PostWith_Async_DuplicateActor_ThrowsArgumentException()
  {
    var actor = new TestActor();
    Assert.Throws<ArgumentException>(() => actor.PostWith(async () => await Task.Yield(), actor));
  }
#endif

  [Fact]
  public async Task DisposeAsync_WaitsForAllPendingTasks()
  {
    var actor = new TestActor();
    var completed = false;
    actor.Post(() => { Thread.Sleep(10); completed = true; });
    await actor.DisposeAsync();
    Assert.True(completed);
  }

  [Fact]
  public async Task DisposeAsync_MultiplePostedTasks_AllComplete()
  {
    var actor = new TestActor();
    var count = 0;
    actor.Post(() => count++);
    actor.Post(() => count++);
    actor.Post(() => count++);
    await actor.DisposeAsync();
    Assert.Equal(3, count);
  }

  [Fact]
  public async Task Post_AfterDispose_DoesNotExecute()
  {
    var actor = new TestActor();
    await actor.DisposeAsync();
    var executed = false;
    actor.Post(() => executed = true);
    Assert.False(executed);
  }

  [Fact]
  public async Task PostWith_DisposedSelf_DoesNotExecute()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    await actor1.DisposeAsync();
    var executed = false;
    actor1.PostWith(() => executed = true, actor2);
    Assert.False(executed);
  }

  [Fact]
  public async Task PostWith_DisposedOther_DoesNotExecute()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    await actor2.DisposeAsync();
    var executed = false;
    actor1.PostWith(() => executed = true, actor2);
    Assert.False(executed);
  }

  [Fact]
  public async Task DisposeAsync_CoreThrows_StillCompletes()
  {
    var actor = new ThrowingDisposeActor();
    await actor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
  }

#if DEBUG
  [Fact]
  public void EnsureThreadSafe_OutsideTask_Throws()
  {
    var actor = new TestActor();
    Assert.Throws<SynchronizationLockException>(() => actor.EnsureThreadSafe());
  }

  [Fact]
  public void EnsureThreadSafe_InsideOwnTask_DoesNotThrow()
  {
    var actor = new TestActor();
    Exception? caught = null;
    actor.Post(() =>
    {
      try
      {
        actor.EnsureThreadSafe();
      }
      catch (Exception e)
      {
        caught = e;
      }
    });
    Assert.Null(caught);
  }

  [Fact]
  public void EnsureThreadSafe_InsideOtherActorTask_Throws()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    Exception? caught = null;
    actor1.Post(() =>
    {
      try
      {
        actor2.EnsureThreadSafe();
      }
      catch (Exception e)
      {
        caught = e;
      }
    });
    Assert.IsType<SynchronizationLockException>(caught);
  }

  [Fact]
  public void EnsureThreadSafe_InsidePostWithTask_DoesNotThrowForJoinedActor()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    Exception? caught = null;
    actor1.PostWith(
      () =>
      {
        try
        {
          actor2.EnsureThreadSafe();
        }
        catch (Exception e)
        {
          caught = e;
        }
      },
      actor2);
    Assert.Null(caught);
  }
#endif

  [Fact]
  public void Post_Async_DispatchToThreadPool_RunsOnPoolThread()
  {
    var actor = new TestActor();
    var ranOnPool = false;
    var completed = new ManualResetEventSlim();
    actor.Post(
      async () =>
      {
        ranOnPool = Thread.CurrentThread.IsThreadPoolThread;
        await Task.Yield();
        completed.Set();
      },
      dispatchToThreadPool: true);
    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    Assert.True(ranOnPool);
  }

  [Fact]
  public void PostWith_Sync_DispatchToThreadPool_RunsOnPoolThread()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var ranOnPool = false;
    var completed = new ManualResetEventSlim();
    actor1.PostWith(
      () =>
      {
        ranOnPool = Thread.CurrentThread.IsThreadPoolThread;
        completed.Set();
      },
      dispatchToThreadPool: true,
      actor2);
    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    Assert.True(ranOnPool);
  }

  [Fact]
  public void PostWith_Async_DispatchToThreadPool_RunsOnPoolThread()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var ranOnPool = false;
    var completed = new ManualResetEventSlim();
    actor1.PostWith(
      async () =>
      {
        ranOnPool = Thread.CurrentThread.IsThreadPoolThread;
        await Task.Yield();
        completed.Set();
      },
      dispatchToThreadPool: true,
      actor2);
    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    Assert.True(ranOnPool);
  }

  [Fact]
  public void Post_Sync_Exception_FiresUnhandledExceptionEvent()
  {
    var actor = new TestActor();
    var captured = new List<Exception>();
    const string uniqueMsg = "post_sync_unhandled_9a3f";
    void Handler(Exception e)
    {
      if (e.Message is uniqueMsg)
      {
        captured.Add(e);
      }
    }

    UnhandledExceptionHandler.UnhandledException += Handler;
    try
    {
      actor.Post(() => throw new InvalidOperationException(uniqueMsg));
    }
    finally
    {
      UnhandledExceptionHandler.UnhandledException -= Handler;
    }

    Assert.Single(captured);
  }

  [Fact]
  public void Post_Async_Exception_FiresUnhandledExceptionEvent()
  {
    var actor = new TestActor();
    var captured = new List<Exception>();
    const string uniqueMsg = "post_async_unhandled_b7c2";
    var completed = new ManualResetEventSlim();
    void Handler(Exception e)
    {
      if (e.Message is uniqueMsg)
      {
        captured.Add(e);
        completed.Set();
      }
    }

    UnhandledExceptionHandler.UnhandledException += Handler;
    try
    {
      actor.Post(async () =>
      {
        await Task.Yield();
        throw new InvalidOperationException(uniqueMsg);
      });
      Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    }
    finally
    {
      UnhandledExceptionHandler.UnhandledException -= Handler;
    }

    Assert.Single(captured);
  }

  [Fact]
  public void Post_Async_SyncThrow_FiresUnhandledExceptionEvent()
  {
    var actor = new TestActor();
    var captured = new List<Exception>();
    const string uniqueMsg = "post_async_syncthrow_c5d1";
    void Handler(Exception e)
    {
      if (e.Message is uniqueMsg)
      {
        captured.Add(e);
      }
    }

    UnhandledExceptionHandler.UnhandledException += Handler;
    try
    {
      Func<Task> func = () => throw new InvalidOperationException(uniqueMsg);
      actor.Post(func);
    }
    finally
    {
      UnhandledExceptionHandler.UnhandledException -= Handler;
    }

    Assert.Single(captured);
  }

  [Fact]
  public async Task DisposeAsync_CoreThrows_FiresUnhandledExceptionEvent()
  {
    var actor = new ThrowingDisposeActor();
    var captured = new List<Exception>();
    const string uniqueMsg = "dispose throw";
    void Handler(Exception e)
    {
      if (e.Message is uniqueMsg)
      {
        captured.Add(e);
      }
    }

    UnhandledExceptionHandler.UnhandledException += Handler;
    try
    {
      await actor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
    finally
    {
      UnhandledExceptionHandler.UnhandledException -= Handler;
    }

    Assert.Single(captured);
  }

  [Fact]
  public async Task DisposeAsync_CalledWhilePostIsRunning_WaitsForCompletion()
  {
    var actor = new TestActor();
    var postStarted = new ManualResetEventSlim();
    var postUnblock = new ManualResetEventSlim();
    var postExecuted = false;

    actor.Post(
      () =>
      {
        postStarted.Set();
        postUnblock.Wait();
        postExecuted = true;
      },
      dispatchToThreadPool: true);

    Assert.True(postStarted.Wait(TimeSpan.FromSeconds(5)));

    var disposeTask = actor.DisposeAsync().AsTask();
    postUnblock.Set();

    await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
    Assert.True(postExecuted);
  }
}
