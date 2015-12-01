namespace SharpCompress.Common
{
    using System;

    public class IncompleteArchiveException : ArchiveException
    {
        public IncompleteArchiveException(string message) : base(message)
        {
        }
    }
}

