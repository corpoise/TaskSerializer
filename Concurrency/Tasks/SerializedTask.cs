namespace Concurrency.Tasks;

using Systems;

internal sealed class SerializedTask : DependencyTaskBase
{
  private readonly Action action;

  public SerializedTask(Dependency dependency, Action action)
    : base(dependency)
  {
    this.action = action;
  }

  public SerializedTask(Dependency[] dependencies, Action action)
    : base(dependencies)
  {
    this.action = action;
  }

  protected override void ExecuteInternal()
  {
    try
    {
      this.action();
    }
    catch (Exception e) when (UnhandledExceptionHandler.OnExceptionOccured(e))
    {
    }

    this.TryExecuteReserved();
  }
}
