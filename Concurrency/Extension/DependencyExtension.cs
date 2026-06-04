namespace Concurrency.Extension;

using Tasks;

internal static class DependencyExtension
{
  public static Dependency[] Flatten(this Dependency[] dependencies)
  {
    if (dependencies.Length <= 1)
    {
      return dependencies;
    }

    if (dependencies.Length is 2)
    {
      if (dependencies[0].Id < dependencies[1].Id)
      {
        return dependencies;
      }

      return [dependencies[1], dependencies[0]];
    }

    Array.Sort(dependencies);

    return dependencies;
  }
}
