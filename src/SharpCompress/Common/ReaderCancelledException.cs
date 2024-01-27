using System;

namespace SharpCompress.Common;

public class ReaderCancelledException : Exception
{
    public ReaderCancelledException(string message)
        : base(message) { }

    public ReaderCancelledException(string message, Exception inner)
        : base(message, inner) { }
}
