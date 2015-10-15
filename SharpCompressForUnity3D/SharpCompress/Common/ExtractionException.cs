namespace SharpCompress.Common
{
    using System;

    public class ExtractionException : Exception
    {
        public ExtractionException(string message) : base(message)
        {
        }

        public ExtractionException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}

