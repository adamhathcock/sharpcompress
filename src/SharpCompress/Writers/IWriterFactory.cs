using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharpCompress.Factories;

namespace SharpCompress.Writers
{
    public interface IWriterFactory : IFactory
    {
        IWriter Open(Stream stream, WriterOptions writerOptions);
    }
}
