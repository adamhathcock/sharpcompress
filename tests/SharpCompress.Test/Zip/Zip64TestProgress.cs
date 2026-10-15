using System;
using System.Diagnostics;
using Xunit;

namespace SharpCompress.Test.Zip;

// Test-only instrumentation: report at most once a second, not once per buffer.
internal sealed class Zip64TestProgress : IDisposable
{
    private readonly ITestOutputHelper output;
    private readonly string phase;
    private readonly long? total;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly int processId;
    private long bytes;
    private double lastReportSeconds;

    public Zip64TestProgress(ITestOutputHelper output, string phase, long? total = null)
    {
        this.output = output;
        this.phase = phase;
        this.total = total;
        using var process = Process.GetCurrentProcess();
        processId = process.Id;
        Report("start");
    }

    public void Advance(int count)
    {
        bytes += count;
        if (stopwatch.Elapsed.TotalSeconds - lastReportSeconds >= 1)
        {
            Report("progress");
        }
    }

    public void Dispose() => Report("end");

    private void Report(string state)
    {
        var seconds = stopwatch.Elapsed.TotalSeconds;
        lastReportSeconds = seconds;
        var rate = seconds > 0 ? bytes / seconds / (1024 * 1024) : 0;
        output.WriteLine(
            $"pid={processId} phase={phase} state={state} bytes={bytes} total={total?.ToString() ?? "unknown"} elapsed={seconds:F3}s rate={rate:F1}MiB/s"
        );
    }
}
