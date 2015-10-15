namespace SharpCompress.Common
{
    using System;

    public class MultipartStreamRequiredException : ExtractionException
    {
        public MultipartStreamRequiredException(string message) : base(message)
        {
        }
    }
}

