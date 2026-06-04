namespace Concurrency.Tasks;

internal sealed class Dependency : IComparable<Dependency>
{
  private static long idCounter = 0;
  private int canAcquire;
  private DependencyTaskBase ownerTask;

  public Dependency()
  {
    this.canAcquire = 0;
    this.ownerTask = DependencyTaskBase.EmptyTask;
    this.Id = Interlocked.Increment(ref idCounter);
  }

  public long Id { get; init; }

  public int CompareTo(Dependency? other)
  {
    if (other is null)
    {
      return 1;
    }

    return this.Id.CompareTo(other.Id);
  }

  public bool TryAcquire()
  {
    return Interlocked.CompareExchange(ref this.canAcquire, 1, 0) is 0;
  }

  public void ReleaseAcquire()
  {
    Interlocked.Exchange(ref this.canAcquire, 0);
  }

  internal DependencyTaskBase TryReserve(DependencyTaskBase task)
  {
    return Interlocked.Exchange(ref this.ownerTask, task);
  }
}
