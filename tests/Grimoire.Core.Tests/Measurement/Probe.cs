using System.Diagnostics;

namespace Grimoire.Core.Tests.Measurement;

public static class Probe
{
    public static Result Measure(int warmup, int repetitions, Action action)
    {
        for (var i = 0; i < warmup; i++)
        {
            action();
        }

        var times = new double[repetitions];
        var allocations = new long[repetitions];

        for (var i = 0; i < repetitions; i++)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            action();
            times[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            allocations[i] = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        }

        return new Result(Median(times), Median(allocations));
    }

    public static async Task<Result> MeasureAsync(int warmup, int repetitions, Func<Task> action)
    {
        for (var i = 0; i < warmup; i++)
        {
            await action();
        }

        var times = new double[repetitions];
        var allocations = new long[repetitions];

        for (var i = 0; i < repetitions; i++)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            await action();
            times[i] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            allocations[i] = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        }

        return new Result(Median(times), Median(allocations));
    }

    private static double Median(double[] values)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
    }

    private static long Median(long[] values)
    {
        var sorted = (long[])values.Clone();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
    }

    public readonly record struct Result(double MedianMilliseconds, long MedianAllocatedBytes);
}
