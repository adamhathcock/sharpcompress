namespace SharpCompress.Compressor.LZMA
{
    using System;

    internal interface ISetCoderProperties
    {
        void SetCoderProperties(CoderPropID[] propIDs, object[] properties);
    }
}

