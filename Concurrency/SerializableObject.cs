namespace Concurrency;

using System.Threading.Tasks;
using Systems;
using Tasks;

public abstract class SerializableObject : IAsyncDisposable
{
  private readonly Dependency dependency;
  private int disposeCalled;

  protected SerializableObject()
  {
    this.dependency = new();
  }

  public ValueTask DisposeAsync()
  {
    if (Interlocked.Exchange(ref this.disposeCalled, 1) is not 0)
    {
      return ValueTask.CompletedTask;
    }

    var tcs = new TaskCompletionSource();
    TaskBuilder.Run(
      async () =>
      {
        try
        {
          await this.DisposeAsyncCore();
        }
        catch (Exception e) when (UnhandledExceptionHandler.OnExceptionOccured(e))
        {
        }

        GC.SuppressFinalize(this);
        tcs.SetResult();
      },
      dispatchToThreadPool: false,
      this.dependency);

    return new ValueTask(tcs.Task);
  }

  public void Post(Action action, bool dispatchToThreadPool = false)
  {
    if (this.disposeCalled is not 0)
    {
      return;
    }

    TaskBuilder.Run(action, dispatchToThreadPool, this.dependency);
  }

  public void Post(Func<Task> func, bool dispatchToThreadPool = false)
  {
    if (this.disposeCalled is not 0)
    {
      return;
    }

    TaskBuilder.Run(func, dispatchToThreadPool, this.dependency);
  }

  public void PostWith(Action action, params SerializableObject[] others)
  {
    this.PostWith(action, false, others);
  }

  public void PostWith(Action action, bool dispatchToThreadPool, params SerializableObject[] others)
  {
    if (this.disposeCalled is not 0)
    {
      return;
    }

    for (var i = 0; i < others.Length; ++i)
    {
      if (others[i].disposeCalled is not 0)
      {
        return;
      }
    }

    TaskBuilder.Run(action, dispatchToThreadPool, this.GatherDependencies(others));
  }

  public void PostWith(Func<Task> func, params SerializableObject[] others)
  {
    this.PostWith(func, false, others);
  }

  public void PostWith(Func<Task> func, bool dispatchToThreadPool, params SerializableObject[] others)
  {
    if (this.disposeCalled is not 0)
    {
      return;
    }

    for (var i = 0; i < others.Length; ++i)
    {
      if (others[i].disposeCalled is not 0)
      {
        return;
      }
    }

    TaskBuilder.Run(func, dispatchToThreadPool, this.GatherDependencies(others));
  }

  public void EnsureThreadSafe()
  {
    DependencyTaskBase.EnsureThreadSafe(this.dependency);
  }

  protected abstract ValueTask DisposeAsyncCore();

  private Dependency[] GatherDependencies(SerializableObject[] others)
  {
    var result = new Dependency[1 + others.Length];
    result[0] = this.dependency;
    for (var i = 0; i < others.Length; ++i)
    {
      result[i + 1] = others[i].dependency;
    }

    return result;
  }
}
