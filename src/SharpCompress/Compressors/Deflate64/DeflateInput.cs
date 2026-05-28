// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SharpCompress.Compressors.Deflate64;

internal sealed class DeflateInput
{
    public DeflateInput(byte[] buffer) => Buffer = buffer;

    public byte[] Buffer { get; }
    public int Count { get; set; }
    public int StartIndex { get; set; }

    internal void ConsumeBytes(int n)
    {
        StartIndex += n;
        Count -= n;
    }

    internal InputState DumpState() => new(Count, StartIndex);

    internal void RestoreState(InputState state)
    {
        Count = state._count;
        StartIndex = state._startIndex;
    }

    internal /*readonly */
    readonly struct InputState
    {
        internal readonly int _count;
        internal readonly int _startIndex;

        internal InputState(int count, int startIndex)
        {
            _count = count;
            _startIndex = startIndex;
        }
    }
}
