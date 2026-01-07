using System;
using System.Text;

namespace SharpCompress.Common;

public static class ArchiveEncodingExtensions
{
    extension(IArchiveEncoding encoding)
    {
        public Encoding GetEncoding(bool useUtf8 = false) =>
            encoding.Forced ?? (useUtf8 ? encoding.UTF8 : encoding.Default);

        public Func<byte[], int, int, string> GetDecoder(bool useUtf8) =>
            encoding.CustomDecoder
            ?? (
                (bytes, index, count) =>
                    encoding.GetEncoding(useUtf8).GetString(bytes, index, count)
            );

        public byte[] Encode(string str) => encoding.Default.GetBytes(str);

        public string Decode(byte[] bytes, bool useUtf8 = false) =>
            encoding.Decode(bytes, 0, bytes.Length, useUtf8);

        public string Decode(byte[] bytes, int start, int length, bool useUtf8 = false) =>
            encoding.GetDecoder(useUtf8)(bytes, start, length);
    }
}
