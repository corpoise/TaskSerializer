namespace Concurrency.Tasks;

using Extension;
using Systems;

internal class DependencyTaskBase
{
  internal static readonly DependencyTaskBase EmptyTask = new();

#if DEBUG
  private const int DepthWarningThreshold = 500;

  private static readonly AsyncLocal<DependencyTaskBase?> RunningTask = new();

  [ThreadStatic]
  private static int callDepth;
#endif

  private readonly Dependency[] dependencies;
  private readonly DependencyTaskBase[] reservedTasks;
  private int requiredDependencyCount;

  protected DependencyTaskBase(Dependency dependency)
    : this([dependency])
  {
  }

  protected DependencyTaskBase(params Dependency[] dependencies)
  {
    var count = dependencies.Length;

#if DEBUG
    if (count is 0)
    {
      throw new ArgumentException("A dependency does not exist.");
    }
#endif

    this.dependencies = dependencies.Flatten();

#if DEBUG
    for (var i = 1; i < count; ++i)
    {
      if (this.dependencies[i].Id == this.dependencies[i - 1].Id)
      {
        throw new ArgumentException("A duplicate dependency exist.");
      }
    }
#endif

    this.reservedTasks = new DependencyTaskBase[count];
    this.requiredDependencyCount = count;
  }

  private DependencyTaskBase()
  {
    this.dependencies = [];
    this.reservedTasks = [];
    this.requiredDependencyCount = 0;
  }

  internal static void EnsureThreadSafe(Dependency dependency)
  {
#if DEBUG
    var running = RunningTask.Value;
    if (running is null)
    {
      throw new SynchronizationLockException("No serialized task is running in the current context.");
    }

    if (Array.BinarySearch(running.dependencies, dependency) < 0)
    {
      throw new SynchronizationLockException("The current task does not hold the required dependency.");
    }
#endif
  }

  internal static void EnsureThreadSafe(params Dependency[] dependencies)
  {
#if DEBUG
    var running = RunningTask.Value;
    if (running is null)
    {
      throw new SynchronizationLockException("No serialized task is running in the current context.");
    }

    foreach (var dep in dependencies)
    {
      if (Array.BinarySearch(running.dependencies, dep) < 0)
      {
        throw new SynchronizationLockException("The current task does not hold the required dependency.");
      }
    }
#endif
  }

  public void TryAcquire(bool dispatchToThreadPool)
  {
    var spinWait = new SpinWait();
    var attemptCount = 0;
    while (this.TryAcquireAll() is false)
    {
      if (++attemptCount is 100)
      {
        spinWait.SpinOnce();
        attemptCount = 0;
      }
    }

    foreach (var dependency in this.dependencies)
    {
      var previousTask = dependency.TryReserve(this);
      if (previousTask == EmptyTask || previousTask.TryAddSucceedTask(this, dependency) is false)
      {
        this.TryExecute(dispatchToThreadPool);
      }

      dependency.ReleaseAcquire();
    }
  }

  private bool TryAcquireAll()
  {
    for (var index = 0; index < this.dependencies.Length; ++index)
    {
      var dependency = this.dependencies[index];
      if (dependency.TryAcquire() is false)
      {
        for (var releaseIndex = 0; releaseIndex < index; ++releaseIndex)
        {
          this.dependencies[releaseIndex].ReleaseAcquire();
        }

        return false;
      }
    }

    return true;
  }

  private bool TryAddSucceedTask(DependencyTaskBase task, Dependency dependency)
  {
    var index = Array.BinarySearch(this.dependencies, dependency);

#if DEBUG
    if (index < 0)
    {
      throw new IndexOutOfRangeException("A dependency does not exist.");
    }
#endif

    return EmptyTask != Interlocked.Exchange(ref this.reservedTasks[index], task);
  }

  private void TryExecute(bool dispatchToThreadPool)
  {
    if (Interlocked.Decrement(ref this.requiredDependencyCount) is not 0)
    {
      return;
    }

    if (dispatchToThreadPool)
    {
      _ = ThreadPool.UnsafeQueueUserWorkItem(static (DependencyTaskBase state) => state.RunInternal(), this, preferLocal: false);

      return;
    }

    this.RunInternal();
  }

  private void RunInternal()
  {
#if DEBUG
    if (++callDepth is DepthWarningThreshold + 1)
    {
      LogProvider.Logger.Warn(
        "Serialized task chain depth exceeded {Threshold} on thread {ThreadId}. Risk of stack overflow.",
        DepthWarningThreshold,
        Environment.CurrentManagedThreadId);
    }

    RunningTask.Value = this;
    try
    {
      this.ExecuteInternal();
    }
    finally
    {
      RunningTask.Value = null;
      --callDepth;
    }
#else
    this.ExecuteInternal();
#endif
  }

  protected virtual void ExecuteInternal()
  {
  }

  protected void TryExecuteReserved()
  {
    for (var index = 0; index < this.dependencies.Length; ++index)
    {
      var nextTask = Interlocked.Exchange(ref this.reservedTasks[index], EmptyTask);
      if (nextTask is null)
      {
        continue;
      }

      nextTask.TryExecute(false);
    }
  }
}
