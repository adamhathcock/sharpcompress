using System;
using JetBrains.Profiler.SelfApi;

namespace SharpCompress.Test;

public static class JetbrainsProfiler
{
    private sealed class CpuClass : IDisposable
    {
        public CpuClass(string snapshotPath)
        {
            DotTrace.Init();
            var config2 = new DotTrace.Config();
            config2.SaveToDir(snapshotPath);
            DotTrace.Attach(config2);
            DotTrace.StartCollectingData();
        }

        public void Dispose()
        {
            DotTrace.StopCollectingData();
            DotTrace.SaveData();
            DotTrace.Detach();
        }
    }

    private sealed class MemoryClass : IDisposable
    {
        public MemoryClass(string snapshotPath)
        {
            DotMemory.Init();
            var config = new DotMemory.Config();
            config.UseLogLevelVerbose();
            config.SaveToDir(snapshotPath);
            DotMemory.Attach(config);
            DotMemory.GetSnapshot("Before");
        }

        public void Dispose()
        {
            DotMemory.GetSnapshot("After");
            DotMemory.Detach();
        }
    }

    public static IDisposable Cpu(string snapshotPath) => new CpuClass(snapshotPath);

    public static IDisposable Memory(string snapshotPath) => new MemoryClass(snapshotPath);
}
