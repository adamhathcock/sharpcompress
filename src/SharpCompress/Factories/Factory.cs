using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Factories;

/// <inheritdoc/>
public abstract class Factory : IFactory
{
    static Factory()
    {
        RegisterFactory(new ZipFactory());
        RegisterFactory(new RarFactory());
        RegisterFactory(new SevenZipFactory());
        RegisterFactory(new GZipFactory());
        RegisterFactory(new TarFactory());
    }

    private static readonly HashSet<Factory> _factories = new();

    /// <summary>
    /// Gets the collection of registered <see cref="IFactory"/>.
    /// </summary>
    public static IEnumerable<IFactory> Factories => _factories;

    /// <summary>
    /// Registers an archive factory.
    /// </summary>
    /// <param name="factory">The factory to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> must not be null.</exception>
    public static void RegisterFactory(Factory factory)
    {
        factory.CheckNotNull(nameof(factory));

        _factories.Add(factory);
    }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public virtual ArchiveType? KnownArchiveType => null;

    /// <inheritdoc/>
    public abstract IEnumerable<string> GetSupportedExtensions();

    /// <inheritdoc/>
    public abstract bool IsArchive(Stream stream, string? password = null);

    /// <inheritdoc/>
    public virtual FileInfo? GetFilePart(int index, FileInfo part1) => null;

    /// <summary>
    /// Tries to open an <see cref="IReader"/> from a <see cref="RewindableStream"/>.
    /// </summary>
    /// <remarks>
    /// This method provides extra insight to support loading compressed TAR files.
    /// </remarks>
    /// <param name="rewindableStream"></param>
    /// <param name="options"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    internal virtual bool TryOpenReader(
        RewindableStream rewindableStream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;

        if (this is IReaderFactory readerFactory)
        {
            rewindableStream.Rewind(false);
            if (IsArchive(rewindableStream, options.Password))
            {
                rewindableStream.Rewind(true);
                reader = readerFactory.OpenReader(rewindableStream, options);
                return true;
            }
        }

        return false;
    }
}
