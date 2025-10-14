using System;

namespace SharpCompress.Common;

public class SharpCompressException : Exception
{
    public SharpCompressException() { }

    public SharpCompressException(string message)
        : base(message) { }

    public SharpCompressException(string message, Exception inner)
        : base(message, inner) { }
}

public class ArchiveException(string message) : SharpCompressException(message);

public class IncompleteArchiveException(string message) : ArchiveException(message);

public class CryptographicException(string message) : SharpCompressException(message);

public class ReaderCancelledException(string message) : SharpCompressException(message);

public class ExtractionException : SharpCompressException
{
    public ExtractionException() { }

    public ExtractionException(string message)
        : base(message) { }

    public ExtractionException(string message, Exception inner)
        : base(message, inner) { }
}

public class MultipartStreamRequiredException(string message) : ExtractionException(message);

public class MultiVolumeExtractionException(string message) : ExtractionException(message);

public class InvalidFormatException : ExtractionException
{
    public InvalidFormatException() { }

    public InvalidFormatException(string message)
        : base(message) { }

    public InvalidFormatException(string message, Exception inner)
        : base(message, inner) { }
}
