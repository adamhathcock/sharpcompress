using System;

namespace SharpCompress.Common;

public interface IVolume : IDisposable, IAsyncDisposable
{
    int Index { get; }

    string? FileName { get; }
}
