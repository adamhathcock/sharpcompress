using System;

namespace SharpCompress.Common
{
    public class IncompleteArchiveException : ArchiveException
    {
        public IncompleteArchiveException(string message)
            : base(message)
        {
        }

        public IncompleteArchiveException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}