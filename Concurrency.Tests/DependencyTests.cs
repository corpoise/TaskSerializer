namespace Concurrency.Tests;

using Tasks;

public sealed class DependencyTests
{
  [Fact]
  public void TryAcquire_FirstCall_ReturnsTrue()
  {
    var dep = new Dependency();
    Assert.True(dep.TryAcquire());
    dep.ReleaseAcquire();
  }

  [Fact]
  public void TryAcquire_WhileAlreadyAcquired_ReturnsFalse()
  {
    var dep = new Dependency();
    dep.TryAcquire();
    Assert.False(dep.TryAcquire());
    dep.ReleaseAcquire();
  }

  [Fact]
  public void ReleaseAcquire_AfterRelease_AllowsReacquire()
  {
    var dep = new Dependency();
    dep.TryAcquire();
    dep.ReleaseAcquire();
    Assert.True(dep.TryAcquire());
    dep.ReleaseAcquire();
  }

  [Fact]
  public void Id_EachInstance_IsUnique()
  {
    var dep1 = new Dependency();
    var dep2 = new Dependency();
    Assert.NotEqual(dep1.Id, dep2.Id);
  }

  [Fact]
  public void Id_EachInstance_IsMonotonicallyIncreasing()
  {
    var dep1 = new Dependency();
    var dep2 = new Dependency();
    Assert.True(dep2.Id > dep1.Id);
  }

  [Fact]
  public void CompareTo_LowerId_ReturnsNegative()
  {
    var dep1 = new Dependency();
    var dep2 = new Dependency();
    Assert.True(dep1.CompareTo(dep2) < 0);
  }

  [Fact]
  public void CompareTo_HigherId_ReturnsPositive()
  {
    var dep1 = new Dependency();
    var dep2 = new Dependency();
    Assert.True(dep2.CompareTo(dep1) > 0);
  }

  [Fact]
  public void CompareTo_SameInstance_ReturnsZero()
  {
    var dep = new Dependency();
    Assert.Equal(0, dep.CompareTo(dep));
  }

  [Fact]
  public void CompareTo_Null_ReturnsPositive()
  {
    var dep = new Dependency();
    Assert.True(dep.CompareTo(null) > 0);
  }
}
