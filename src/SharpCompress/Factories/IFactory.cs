using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Factories
{
    /// <summary>
    /// Represents the foundation of an archive factory.
    /// </summary>
    /// <remarks>
    /// To get extended functionality, this type can be cast to:<br/>
    /// <see cref="Archives.IArchiveFactory"/><br/>
    /// <see cref="Readers.IReaderFactory"/><br/>
    /// <see cref="Writers.IWriterFactory"/><br/>
    /// </remarks>
    public interface IFactory
    {
        /// <summary>
        /// Gets the archive Type name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// returns the extensions typically used by this archive type.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetSupportedExtensions();

        /// <summary>
        /// returns the archive type in case it is a well known archive.
        /// </summary>
        Common.ArchiveType? KnownArchiveType { get; }

        /// <summary>
        /// Returns true if the stream represents an archive of the format defined by this type.
        /// </summary>
        /// <param name="stream">A stream, pointing to the beginning of the archive.</param>
        /// <param name="password">optional password</param>
        bool IsArchive(Stream stream, string? password = null);
    }
}

