namespace SharpCompress.Writer
{
    using System;
    using System.IO;

    public static class IWriterExtensions
    {
        public static void Write(IWriter writer, string entryPath, Stream source)
        {
            writer.Write(entryPath, source, null);
        }
    }
}

