namespace SharpCompress.Compressor.LZMA.Utilites
{
    using System;

    internal interface IPasswordProvider
    {
        string CryptoGetTextPassword();
    }
}

