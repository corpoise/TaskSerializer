namespace Concurrency;

using System.Threading.Tasks;
using Tasks;

internal static class TaskBuilder
{
  internal static void Run(Action action, bool dispatchToThreadPool, Dependency dependency)
  {
    var task = new SerializedTask(dependency, action);
    task.TryAcquire(dispatchToThreadPool);
  }

  internal static void Run(Action action, bool dispatchToThreadPool, params Dependency[] dependencies)
  {
    var task = new SerializedTask(dependencies, action);
    task.TryAcquire(dispatchToThreadPool);
  }

  internal static void Run(Func<Task> func, bool dispatchToThreadPool, Dependency dependency)
  {
    var task = new AwaitableSerializedTask(dependency, func);
    task.TryAcquire(dispatchToThreadPool);
  }

  internal static void Run(Func<Task> func, bool dispatchToThreadPool, params Dependency[] dependencies)
  {
    var task = new AwaitableSerializedTask(dependencies, func);
    task.TryAcquire(dispatchToThreadPool);
  }
}
