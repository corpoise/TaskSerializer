namespace Concurrency.Tests;

using NLog;

public sealed class SerializationTests
{
  private sealed class TestActor : SerializableObject
  {
    protected override ValueTask DisposeAsyncCore()
    {
      return ValueTask.CompletedTask;
    }
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(4)]
  [InlineData(5)]
  [InlineData(6)]
  [InlineData(7)]
  [InlineData(8)]
  [InlineData(9)]
  [InlineData(10)]
  public void SharedSingleDep_NSyncTasks_RunSerially(int taskCount)
  {
    var actor = new TestActor();
    var running = 0;
    var violations = 0;
    var executed = 0;
    var completed = new CountdownEvent(taskCount);

    for (var i = 0; i < taskCount; i++)
    {
      ThreadPool.QueueUserWorkItem(_ =>
      {
        actor.Post(() =>
        {
          if (Interlocked.Increment(ref running) > 1)
          {
            Interlocked.Increment(ref violations);
          }

          Thread.SpinWait(10_000);
          Interlocked.Increment(ref executed);
          Interlocked.Decrement(ref running);
          completed.Signal();
        });
      });
    }

    Assert.True(completed.Wait(TimeSpan.FromSeconds(10)));
    Assert.Equal(taskCount, executed);
    Assert.Equal(0, violations);
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(4)]
  [InlineData(5)]
  [InlineData(6)]
  [InlineData(7)]
  [InlineData(8)]
  [InlineData(9)]
  [InlineData(10)]
  public void SharedSingleDep_NAsyncTasks_RunSerially(int taskCount)
  {
    var actor = new TestActor();
    var running = 0;
    var violations = 0;
    var completed = new CountdownEvent(taskCount);

    for (var i = 0; i < taskCount; i++)
    {
      actor.Post(async () =>
      {
        if (Interlocked.Increment(ref running) > 1)
        {
          Interlocked.Increment(ref violations);
        }

        await Task.Yield();
        Interlocked.Decrement(ref running);
        completed.Signal();
      });
    }

    Assert.True(completed.Wait(TimeSpan.FromSeconds(10)));
    Assert.Equal(0, violations);
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(4)]
  [InlineData(5)]
  [InlineData(6)]
  [InlineData(7)]
  [InlineData(8)]
  [InlineData(9)]
  [InlineData(10)]
  public void SharedMultipleDeps_NSyncTasks_RunSerially(int taskCount)
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var running = 0;
    var violations = 0;
    var executed = 0;
    var completed = new CountdownEvent(taskCount);

    for (var i = 0; i < taskCount; i++)
    {
      ThreadPool.QueueUserWorkItem(_ =>
      {
        actor1.PostWith(() =>
        {
          if (Interlocked.Increment(ref running) > 1)
          {
            Interlocked.Increment(ref violations);
          }

          Thread.SpinWait(10_000);
          Interlocked.Increment(ref executed);
          Interlocked.Decrement(ref running);
          completed.Signal();
        }, actor2);
      });
    }

    Assert.True(completed.Wait(TimeSpan.FromSeconds(10)));
    Assert.Equal(taskCount, executed);
    Assert.Equal(0, violations);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(4)]
  [InlineData(5)]
  [InlineData(6)]
  [InlineData(7)]
  [InlineData(8)]
  [InlineData(9)]
  [InlineData(10)]
  public void SharedNDeps_SyncTasks_RunSerially(int depCount)
  {
    var actors = Enumerable.Range(0, depCount).Select(_ => new TestActor()).ToArray();
    var running = 0;
    var violations = 0;
    var executed = 0;
    const int taskCount = 5;
    var completed = new CountdownEvent(taskCount);

    for (var i = 0; i < taskCount; i++)
    {
      ThreadPool.QueueUserWorkItem(_ =>
      {
        actors[0].PostWith(() =>
        {
          if (Interlocked.Increment(ref running) > 1)
          {
            Interlocked.Increment(ref violations);
          }

          Thread.SpinWait(10_000);
          Interlocked.Increment(ref executed);
          Interlocked.Decrement(ref running);
          completed.Signal();
        }, actors[1..]);
      });
    }

    Assert.True(completed.Wait(TimeSpan.FromSeconds(10)));
    Assert.Equal(taskCount, executed);
    Assert.Equal(0, violations);
  }

  [Theory]
  [InlineData(1)]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(4)]
  [InlineData(5)]
  [InlineData(6)]
  [InlineData(7)]
  [InlineData(8)]
  [InlineData(9)]
  [InlineData(10)]
  public void SharedNDeps_AsyncTasks_RunSerially(int depCount)
  {
    var actors = Enumerable.Range(0, depCount).Select(_ => new TestActor()).ToArray();
    var running = 0;
    var violations = 0;
    const int taskCount = 5;
    var completed = new CountdownEvent(taskCount);

    for (var i = 0; i < taskCount; i++)
    {
      actors[0].PostWith(async () =>
      {
        if (Interlocked.Increment(ref running) > 1)
        {
          Interlocked.Increment(ref violations);
        }

        await Task.Yield();
        Interlocked.Decrement(ref running);
        completed.Signal();
      }, actors[1..]);
    }

    Assert.True(completed.Wait(TimeSpan.FromSeconds(10)));
    Assert.Equal(0, violations);
  }

  [Fact]
  public void FiveDeps_TenThousandSyncTasks_RunSerially()
  {
    var actors = Enumerable.Range(0, 5).Select(_ => new TestActor()).ToArray();
    var running = 0;
    var violations = 0;
    var executed = 0;
    const int taskCount = 10_000;
    var completed = new CountdownEvent(taskCount);

    for (var i = 0; i < taskCount; i++)
    {
      ThreadPool.QueueUserWorkItem(_ =>
      {
        actors[0].PostWith(() =>
        {
          if (Interlocked.Increment(ref running) > 1)
          {
            Interlocked.Increment(ref violations);
          }

          Interlocked.Increment(ref executed);
          Interlocked.Decrement(ref running);
          completed.Signal();
        }, actors[1..]);
      });
    }

    Assert.True(completed.Wait(TimeSpan.FromSeconds(30)));
    Assert.Equal(taskCount, executed);
    Assert.Equal(0, violations);
  }

  [Fact]
  public void FiveDeps_TenThousandAsyncTasks_RunSerially()
  {
    var actors = Enumerable.Range(0, 5).Select(_ => new TestActor()).ToArray();
    var running = 0;
    var violations = 0;
    const int taskCount = 10_000;
    var completed = new CountdownEvent(taskCount);

    for (var i = 0; i < taskCount; i++)
    {
      actors[0].PostWith(async () =>
      {
        if (Interlocked.Increment(ref running) > 1)
        {
          Interlocked.Increment(ref violations);
        }

        await Task.Yield();
        Interlocked.Decrement(ref running);
        completed.Signal();
      }, actors[1..]);
    }

    Assert.True(completed.Wait(TimeSpan.FromSeconds(30)));
    Assert.Equal(0, violations);
  }

  [Fact]
  public void SharedDep_SequentialSync_ExecutesInOrder()
  {
    var actor = new TestActor();
    var order = new List<int>();

    actor.Post(() => order.Add(1));
    actor.Post(() => order.Add(2));
    actor.Post(() => order.Add(3));

    Assert.Equal([1, 2, 3], order);
  }

  [Fact]
  public void SharedDep_SequentialAsync_ExecutesInOrder()
  {
    var actor = new TestActor();
    var order = new List<int>();
    var completed = new CountdownEvent(3);

    actor.Post(async () => { await Task.Yield(); order.Add(1); completed.Signal(); });
    actor.Post(async () => { await Task.Yield(); order.Add(2); completed.Signal(); });
    actor.Post(async () => { await Task.Yield(); order.Add(3); completed.Signal(); });

    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    Assert.Equal([1, 2, 3], order);
  }

  [Fact]
  public void MixedPostAndPostWith_ConcurrentCalls_RunSerially()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var running = 0;
    var violations = 0;
    var executed = 0;
    const int taskCount = 100;
    var completed = new CountdownEvent(taskCount);

    for (var i = 0; i < taskCount; i++)
    {
      if (i % 2 is 0)
      {
        ThreadPool.QueueUserWorkItem(_ =>
        {
          actor1.Post(() =>
          {
            if (Interlocked.Increment(ref running) > 1)
            {
              Interlocked.Increment(ref violations);
            }

            Thread.SpinWait(1_000);
            Interlocked.Increment(ref executed);
            Interlocked.Decrement(ref running);
            completed.Signal();
          });
        });
      }
      else
      {
        ThreadPool.QueueUserWorkItem(_ =>
        {
          actor1.PostWith(() =>
          {
            if (Interlocked.Increment(ref running) > 1)
            {
              Interlocked.Increment(ref violations);
            }

            Thread.SpinWait(1_000);
            Interlocked.Increment(ref executed);
            Interlocked.Decrement(ref running);
            completed.Signal();
          }, actor2);
        });
      }
    }

    Assert.True(completed.Wait(TimeSpan.FromSeconds(30)));
    Assert.Equal(taskCount, executed);
    Assert.Equal(0, violations);
  }

  [Fact]
  public void OverlappingDeps_SyncTasks_RunSerially()
  {
    var actorA = new TestActor();
    var actorB = new TestActor();
    var actorC = new TestActor();
    var actorD = new TestActor();
    var running = 0;
    var violations = 0;
    var executed = 0;
    const int taskCount = 30;
    var completed = new CountdownEvent(taskCount * 3);

    Action MakeAction()
    {
      return () =>
      {
        if (Interlocked.Increment(ref running) > 1)
        {
          Interlocked.Increment(ref violations);
        }

        Thread.SpinWait(1_000);
        Interlocked.Increment(ref executed);
        Interlocked.Decrement(ref running);
        completed.Signal();
      };
    }

    for (var i = 0; i < taskCount; i++)
    {
      ThreadPool.QueueUserWorkItem(_ => actorA.PostWith(MakeAction(), actorB, actorC));
      ThreadPool.QueueUserWorkItem(_ => actorA.PostWith(MakeAction(), actorB, actorD));
      ThreadPool.QueueUserWorkItem(_ => actorA.PostWith(MakeAction(), actorC, actorD));
    }

    Assert.True(completed.Wait(TimeSpan.FromSeconds(30)));
    Assert.Equal(taskCount * 3, executed);
    Assert.Equal(0, violations);
  }

  [Fact]
  public void IndependentDeps_SyncTasks_RunIndependently()
  {
    var actor1 = new TestActor();
    var actor2 = new TestActor();
    var count = 0;
    var completed = new CountdownEvent(2);

    ThreadPool.QueueUserWorkItem(_ =>
    {
      actor1.Post(() =>
      {
        Interlocked.Increment(ref count);
        completed.Signal();
      });
    });

    ThreadPool.QueueUserWorkItem(_ =>
    {
      actor2.Post(() =>
      {
        Interlocked.Increment(ref count);
        completed.Signal();
      });
    });

    Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
    Assert.Equal(2, count);
  }

  [Fact]
  public void SingleDep_HundredThousandSyncTasks_RunSerially()
  {
    var actor = new TestActor();
    var running = 0;
    var violations = 0;
    var executed = 0;
    const int taskCount = 100_000;
    var concurrency = Environment.ProcessorCount * 2;
    var perThread = taskCount / concurrency;
    var actualTaskCount = perThread * concurrency;
    var completed = new CountdownEvent(actualTaskCount);
    var barrier = new Barrier(concurrency);

    LogManager.SuspendLogging();
    try
    {
      for (var t = 0; t < concurrency; t++)
      {
        ThreadPool.QueueUserWorkItem(_ =>
        {
          barrier.SignalAndWait();

          for (var i = 0; i < perThread; i++)
          {
            actor.Post(() =>
            {
              if (Interlocked.Increment(ref running) > 1)
              {
                Interlocked.Increment(ref violations);
              }

              Interlocked.Increment(ref executed);
              Interlocked.Decrement(ref running);
              completed.Signal();
            });
          }
        });
      }

      Assert.True(completed.Wait(TimeSpan.FromSeconds(60)));
    }
    finally
    {
      LogManager.ResumeLogging();
    }

    Assert.Equal(actualTaskCount, executed);
    Assert.Equal(0, violations);
  }

  [Fact]
  public void ThreeDeps_HundredThousandSyncTasks_RunSerially()
  {
    var actors = Enumerable.Range(0, 3).Select(_ => new TestActor()).ToArray();
    var running = 0;
    var violations = 0;
    var executed = 0;
    const int taskCount = 100_000;
    var concurrency = Environment.ProcessorCount * 2;
    var perThread = taskCount / concurrency;
    var actualTaskCount = perThread * concurrency;
    var completed = new CountdownEvent(actualTaskCount);
    var barrier = new Barrier(concurrency);

    LogManager.SuspendLogging();
    try
    {
      for (var t = 0; t < concurrency; t++)
      {
        ThreadPool.QueueUserWorkItem(_ =>
        {
          barrier.SignalAndWait();

          for (var i = 0; i < perThread; i++)
          {
            actors[0].PostWith(() =>
            {
              if (Interlocked.Increment(ref running) > 1)
              {
                Interlocked.Increment(ref violations);
              }

              Interlocked.Increment(ref executed);
              Interlocked.Decrement(ref running);
              completed.Signal();
            }, actors[1], actors[2]);
          }
        });
      }

      Assert.True(completed.Wait(TimeSpan.FromSeconds(60)));
    }
    finally
    {
      LogManager.ResumeLogging();
    }

    Assert.Equal(actualTaskCount, executed);
    Assert.Equal(0, violations);
  }

  [Fact]
  public void CrossActorPostWith_HundredThousandTasks_RunSerially()
  {
    var actorA = new TestActor();
    var actorB = new TestActor();
    var actorC = new TestActor();
    var runningA = 0;
    var runningB = 0;
    var runningC = 0;
    var violations = 0;
    const int taskCount = 100_000;
    var concurrency = Environment.ProcessorCount * 2;
    var perThread = taskCount / concurrency;
    var actualTaskCount = perThread * concurrency;
    var completed = new CountdownEvent(actualTaskCount);
    var barrier = new Barrier(concurrency);

    LogManager.SuspendLogging();
    try
    {
      for (var t = 0; t < concurrency; t++)
      {
        ThreadPool.QueueUserWorkItem(_ =>
        {
          barrier.SignalAndWait();

          for (var i = 0; i < perThread; i++)
          {
            switch (i % 3)
            {
              case 0:
                actorA.PostWith(() =>
                {
                  var ra = Interlocked.Increment(ref runningA);
                  var rb = Interlocked.Increment(ref runningB);
                  if (ra > 1 || rb > 1)
                  {
                    Interlocked.Increment(ref violations);
                  }

                  Interlocked.Decrement(ref runningA);
                  Interlocked.Decrement(ref runningB);
                  completed.Signal();
                }, actorB);

                break;
              case 1:
                actorB.PostWith(() =>
                {
                  var rb = Interlocked.Increment(ref runningB);
                  var rc = Interlocked.Increment(ref runningC);
                  if (rb > 1 || rc > 1)
                  {
                    Interlocked.Increment(ref violations);
                  }

                  Interlocked.Decrement(ref runningB);
                  Interlocked.Decrement(ref runningC);
                  completed.Signal();
                }, actorC);

                break;
              default:
                actorA.PostWith(() =>
                {
                  var ra = Interlocked.Increment(ref runningA);
                  var rc = Interlocked.Increment(ref runningC);
                  if (ra > 1 || rc > 1)
                  {
                    Interlocked.Increment(ref violations);
                  }

                  Interlocked.Decrement(ref runningA);
                  Interlocked.Decrement(ref runningC);
                  completed.Signal();
                }, actorC);

                break;
            }
          }
        });
      }

      Assert.True(completed.Wait(TimeSpan.FromSeconds(60)));
    }
    finally
    {
      LogManager.ResumeLogging();
    }

    Assert.Equal(0, violations);
  }

  [Fact]
  public void SingleDep_HundredThousandSyncTasks_Direct_RunSerially()
  {
    var actor = new TestActor();
    var running = 0;
    var violations = 0;
    var executed = 0;
    const int taskCount = 100_000;
    var completed = new CountdownEvent(taskCount);

    LogManager.SuspendLogging();
    try
    {
      for (var i = 0; i < taskCount; i++)
      {
        actor.Post(() =>
        {
          if (Interlocked.Increment(ref running) > 1)
          {
            Interlocked.Increment(ref violations);
          }

          Interlocked.Increment(ref executed);
          Interlocked.Decrement(ref running);
          completed.Signal();
        });
      }

      Assert.True(completed.Wait(TimeSpan.FromSeconds(60)));
    }
    finally
    {
      LogManager.ResumeLogging();
    }

    Assert.Equal(taskCount, executed);
    Assert.Equal(0, violations);
  }

  [Fact]
  public void ThreeDeps_HundredThousandSyncTasks_Direct_RunSerially()
  {
    var actors = Enumerable.Range(0, 3).Select(_ => new TestActor()).ToArray();
    var running = 0;
    var violations = 0;
    var executed = 0;
    const int taskCount = 100_000;
    var completed = new CountdownEvent(taskCount);

    LogManager.SuspendLogging();
    try
    {
      for (var i = 0; i < taskCount; i++)
      {
        actors[0].PostWith(() =>
        {
          if (Interlocked.Increment(ref running) > 1)
          {
            Interlocked.Increment(ref violations);
          }

          Interlocked.Increment(ref executed);
          Interlocked.Decrement(ref running);
          completed.Signal();
        }, actors[1], actors[2]);
      }

      Assert.True(completed.Wait(TimeSpan.FromSeconds(60)));
    }
    finally
    {
      LogManager.ResumeLogging();
    }

    Assert.Equal(taskCount, executed);
    Assert.Equal(0, violations);
  }

  [Fact]
  public void CrossActorPostWith_HundredThousandTasks_Direct_RunSerially()
  {
    var actorA = new TestActor();
    var actorB = new TestActor();
    var actorC = new TestActor();
    var runningA = 0;
    var runningB = 0;
    var runningC = 0;
    var violations = 0;
    const int taskCount = 100_000;
    var completed = new CountdownEvent(taskCount);

    LogManager.SuspendLogging();
    try
    {
      for (var i = 0; i < taskCount; i++)
      {
        switch (i % 3)
        {
          case 0:
            actorA.PostWith(() =>
            {
              var ra = Interlocked.Increment(ref runningA);
              var rb = Interlocked.Increment(ref runningB);
              if (ra > 1 || rb > 1)
              {
                Interlocked.Increment(ref violations);
              }

              Interlocked.Decrement(ref runningA);
              Interlocked.Decrement(ref runningB);
              completed.Signal();
            }, actorB);

            break;
          case 1:
            actorB.PostWith(() =>
            {
              var rb = Interlocked.Increment(ref runningB);
              var rc = Interlocked.Increment(ref runningC);
              if (rb > 1 || rc > 1)
              {
                Interlocked.Increment(ref violations);
              }

              Interlocked.Decrement(ref runningB);
              Interlocked.Decrement(ref runningC);
              completed.Signal();
            }, actorC);

            break;
          default:
            actorA.PostWith(() =>
            {
              var ra = Interlocked.Increment(ref runningA);
              var rc = Interlocked.Increment(ref runningC);
              if (ra > 1 || rc > 1)
              {
                Interlocked.Increment(ref violations);
              }

              Interlocked.Decrement(ref runningA);
              Interlocked.Decrement(ref runningC);
              completed.Signal();
            }, actorC);

            break;
        }
      }

      Assert.True(completed.Wait(TimeSpan.FromSeconds(60)));
    }
    finally
    {
      LogManager.ResumeLogging();
    }

    Assert.Equal(0, violations);
  }

}
