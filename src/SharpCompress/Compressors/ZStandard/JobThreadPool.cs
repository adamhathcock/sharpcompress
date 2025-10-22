using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SharpCompress.Compressors.ZStandard;

internal unsafe class JobThreadPool : IDisposable
{
    private int numThreads;
    private readonly List<JobThread> threads;
    private readonly BlockingCollection<Job> queue;

    private struct Job
    {
        public void* function;
        public void* opaque;
    }

    private class JobThread
    {
        private Thread Thread { get; }
        public CancellationTokenSource CancellationTokenSource { get; }

        public JobThread(Thread thread)
        {
            CancellationTokenSource = new CancellationTokenSource();
            Thread = thread;
        }

        public void Start()
        {
            Thread.Start(this);
        }

        public void Cancel()
        {
            CancellationTokenSource.Cancel();
        }

        public void Join()
        {
            Thread.Join();
        }
    }

    private void Worker(object? obj)
    {
        if (obj is not JobThread poolThread)
            return;

        var cancellationToken = poolThread.CancellationTokenSource.Token;
        while (!queue.IsCompleted && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (queue.TryTake(out var job, -1, cancellationToken))
                    ((delegate* managed<void*, void>)job.function)(job.opaque);
            }
            catch (InvalidOperationException) { }
            catch (OperationCanceledException) { }
        }
    }

    public JobThreadPool(int num, int queueSize)
    {
        numThreads = num;
        queue = new BlockingCollection<Job>(queueSize + 1);
        threads = new List<JobThread>(num);
        for (var i = 0; i < numThreads; i++)
            CreateThread();
    }

    private void CreateThread()
    {
        var poolThread = new JobThread(new Thread(Worker));
        threads.Add(poolThread);
        poolThread.Start();
    }

    public void Resize(int num)
    {
        lock (threads)
        {
            if (num < numThreads)
            {
                for (var i = numThreads - 1; i >= num; i--)
                {
                    threads[i].Cancel();
                    threads.RemoveAt(i);
                }
            }
            else
            {
                for (var i = numThreads; i < num; i++)
                    CreateThread();
            }
        }

        numThreads = num;
    }

    public void Add(void* function, void* opaque)
    {
        queue.Add(new Job { function = function, opaque = opaque });
    }

    public bool TryAdd(void* function, void* opaque)
    {
        return queue.TryAdd(new Job { function = function, opaque = opaque });
    }

    public void Join(bool cancel = true)
    {
        queue.CompleteAdding();
        List<JobThread> jobThreads;
        lock (threads)
            jobThreads = new List<JobThread>(threads);

        if (cancel)
        {
            foreach (var thread in jobThreads)
                thread.Cancel();
        }

        foreach (var thread in jobThreads)
            thread.Join();
    }

    public void Dispose()
    {
        queue.Dispose();
    }

    public int Size()
    {
        // todo not implemented
        // https://github.com/dotnet/runtime/issues/24200
        return 0;
    }
}
