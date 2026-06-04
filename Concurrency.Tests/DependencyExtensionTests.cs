namespace Concurrency.Tests;

using Extension;
using Tasks;

public sealed class DependencyExtensionTests
{
  [Fact]
  public void Flatten_LengthZero_ReturnsSameInstance()
  {
    var input = Array.Empty<Dependency>();
    var output = input.Flatten();
    Assert.Same(input, output);
  }

  [Fact]
  public void Flatten_LengthOne_ReturnsSameInstance()
  {
    var input = new[] { new Dependency() };
    var output = input.Flatten();
    Assert.Same(input, output);
  }

  [Fact]
  public void Flatten_LengthTwo_AlreadySorted_ReturnsSameInstance()
  {
    var dep1 = new Dependency();
    var dep2 = new Dependency();
    var input = new[] { dep1, dep2 };
    var output = input.Flatten();
    Assert.Same(input, output);
  }

  [Fact]
  public void Flatten_LengthTwo_Reversed_ReturnsNewSortedArray()
  {
    var dep1 = new Dependency();
    var dep2 = new Dependency();
    var input = new[] { dep2, dep1 };
    var output = input.Flatten();
    Assert.NotSame(input, output);
    Assert.Same(dep1, output[0]);
    Assert.Same(dep2, output[1]);
  }

  [Fact]
  public void Flatten_LengthThreePlus_ReturnsSortedSequence()
  {
    var dep1 = new Dependency();
    var dep2 = new Dependency();
    var dep3 = new Dependency();
    var input = new[] { dep3, dep1, dep2 };
    var output = input.Flatten();
    Assert.Equal(dep1.Id, output[0].Id);
    Assert.Equal(dep2.Id, output[1].Id);
    Assert.Equal(dep3.Id, output[2].Id);
  }
}
