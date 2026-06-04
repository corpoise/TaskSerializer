namespace Concurrency.Tasks;

using System.Threading.Tasks;
using Systems;

internal sealed class AwaitableSerializedTask : DependencyTaskBase
{
  private readonly Func<Task> func;

  public AwaitableSerializedTask(Dependency dependency, Func<Task> func)
    : base(dependency)
  {
    this.func = func;
  }

  public AwaitableSerializedTask(Dependency[] dependencies, Func<Task> func)
    : base(dependencies)
  {
    this.func = func;
  }

  protected override void ExecuteInternal()
  {
    _ = this.RunAsync();
  }

  private async Task RunAsync()
  {
    try
    {
      await this.func();
    }
    catch (Exception e) when (UnhandledExceptionHandler.OnExceptionOccured(e))
    {
    }

    this.TryExecuteReserved();
  }
}
