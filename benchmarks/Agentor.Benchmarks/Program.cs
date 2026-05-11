using BenchmarkDotNet.Running;

namespace Agentor.Benchmarks;

internal static class BenchmarkEntry
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(BenchmarkEntry).Assembly).Run(args);
}
