using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.GZip;
//using SharpCompress.Archives.Rar;
//using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives
{
    public static class ArchiveFactory
    {
        /// <summary>
        /// Opens an Archive for random access
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="readerOptions"></param>
        /// <returns></returns>
        public static async ValueTask<IArchive> OpenAsync(Stream stream, ReaderOptions? readerOptions = null, CancellationToken cancellationToken = default)
        {
            stream.CheckNotNull(nameof(stream));
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("Stream should be readable and seekable");
            }
            readerOptions ??= new ReaderOptions();
            if (await ZipArchive.IsZipFileAsync(stream, null, cancellationToken))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return ZipArchive.Open(stream, readerOptions);
            }
            stream.Seek(0, SeekOrigin.Begin);
            /*if (SevenZipArchive.IsSevenZipFile(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return SevenZipArchive.Open(stream, readerOptions);
            }
            stream.Seek(0, SeekOrigin.Begin);  */
            if (await GZipArchive.IsGZipFileAsync(stream, cancellationToken))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return GZipArchive.Open(stream, readerOptions);
            }
            stream.Seek(0, SeekOrigin.Begin);
           /* if (RarArchive.IsRarFile(stream, readerOptions))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return RarArchive.Open(stream, readerOptions);
            }
            stream.Seek(0, SeekOrigin.Begin);  */
            if (await TarArchive.IsTarFileAsync(stream, cancellationToken))
            {
                stream.Seek(0, SeekOrigin.Begin);
                return TarArchive.Open(stream, readerOptions);
            }                    
            throw new InvalidOperationException("Cannot determine compressed stream type. Supported Archive Formats: Zip, GZip, Tar, Rar, 7Zip, LZip");
        }

        public static IWritableArchive Create(ArchiveType type)
        {
            return type switch
            {
                ArchiveType.Zip => ZipArchive.Create(),
                //ArchiveType.Tar => TarArchive.Create(),
                ArchiveType.GZip => GZipArchive.Create(),
                _ => throw new NotSupportedException("Cannot create Archives of type: " + type)
            };
        }

        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="options"></param>
        public static ValueTask<IArchive> OpenAsync(string filePath, ReaderOptions? options = null)
        {
            filePath.CheckNotNullOrEmpty(nameof(filePath));
            return OpenAsync(new FileInfo(filePath), options);
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="options"></param>
        public static async ValueTask<IArchive> OpenAsync(FileInfo fileInfo, ReaderOptions? options = null, CancellationToken cancellationToken = default)
        {
            fileInfo.CheckNotNull(nameof(fileInfo));
            options ??= new ReaderOptions { LeaveStreamOpen = false };

            await using var stream = fileInfo.OpenRead();
            if (await ZipArchive.IsZipFileAsync(stream, null, cancellationToken))
            {
                return ZipArchive.Open(fileInfo, options);
            }
            stream.Seek(0, SeekOrigin.Begin);
            /*if (SevenZipArchive.IsSevenZipFile(stream))
            {
                return SevenZipArchive.Open(fileInfo, options);
            }
            stream.Seek(0, SeekOrigin.Begin);     */
            if (await GZipArchive.IsGZipFileAsync(stream, cancellationToken))
            {
                return GZipArchive.Open(fileInfo, options);
            }
            stream.Seek(0, SeekOrigin.Begin);
            /*if (RarArchive.IsRarFile(stream, options))
            {
                return RarArchive.Open(fileInfo, options);
            }
            stream.Seek(0, SeekOrigin.Begin);
            if (TarArchive.IsTarFile(stream))
            {
                return TarArchive.Open(fileInfo, options);
            }                          */
            throw new InvalidOperationException("Cannot determine compressed stream type. Supported Archive Formats: Zip, GZip, Tar, Rar, 7Zip");
        }

        /// <summary>
        /// Extract to specific directory, retaining filename
        /// </summary>
        public static async ValueTask WriteToDirectory(string sourceArchive, 
                                                       string destinationDirectory,
                                                 ExtractionOptions? options = null, 
                                                 CancellationToken cancellationToken = default)
        {
            await using IArchive archive = await OpenAsync(sourceArchive);
            await foreach (IArchiveEntry entry in archive.Entries.WithCancellation(cancellationToken))
            {
                await entry.WriteEntryToDirectoryAsync(destinationDirectory, options, cancellationToken);
            }
        }
    }
}
