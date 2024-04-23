using System;

namespace SharpCompress.Common;

public interface IVolume : IDisposable
{
    int Index { get; }

    string? FileName { get; }
}
