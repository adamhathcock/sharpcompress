namespace SharpCompress.Common
{
    using System;

    public class MultiVolumeExtractionException : ExtractionException
    {
        public MultiVolumeExtractionException(string message) : base(message)
        {
        }

        public MultiVolumeExtractionException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}

