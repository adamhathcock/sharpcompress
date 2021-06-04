using System;

namespace SharpCompress.Common
{
    public class ArchiveException : Exception
    {
        public ArchiveException(string message)
            : base(message)
        {
        }

        public ArchiveException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}