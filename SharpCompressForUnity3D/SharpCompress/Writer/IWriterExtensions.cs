namespace SharpCompress.Writer
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    //[Extension]
    public static class IWriterExtensions
    {
        //[Extension]
        public static void Write(IWriter writer, string entryPath, Stream source)
        {
            writer.Write(entryPath, source, null);
        }
    }
}

