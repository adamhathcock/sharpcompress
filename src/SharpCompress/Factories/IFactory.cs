using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Factories;

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
    /// Gets the archive Type in case it is a well known archive format.
    /// </summary>
    Common.ArchiveType? KnownArchiveType { get; }

    /// <summary>
    /// returns the extensions typically used by this archive type.
    /// </summary>
    /// <returns></returns>
    IEnumerable<string> GetSupportedExtensions();

    /// <summary>
    /// Returns true if the stream represents an archive of the format defined by this type.
    /// </summary>
    /// <param name="stream">A stream, pointing to the beginning of the archive.</param>
    /// <param name="password">optional password</param>
    bool IsArchive(Stream stream, string? password = null);

    /// <summary>
    /// From a passed in archive (zip, rar, 7z, 001), return all parts.
    /// </summary>
    /// <param name="part1">Path to the first part.</param>
    /// <returns>
    /// The path to the requested part,
    /// or NULL if the part does not exist.
    /// </returns>
    FileInfo? GetFilePart(int index, FileInfo part1);
}
