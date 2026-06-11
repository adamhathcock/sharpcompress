using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test;

public class OptionsUsabilityTests : TestBase
{
    [Fact]
    public void ReaderFactory_Stream_Default_Leaves_Stream_Open()
    {
        using var file = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"));
        using var testStream = new TestStream(file);

        using (var reader = ReaderFactory.OpenReader(testStream))
        {
            reader.MoveToNextEntry();
        }

        Assert.False(testStream.IsDisposed);
    }

    [Fact]
    public void ArchiveFactory_Stream_Default_Leaves_Stream_Open()
    {
        using var file = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"));
        using var testStream = new TestStream(file);

        using (var archive = ArchiveFactory.OpenArchive(testStream))
        {
            _ = archive.Entries;
        }

        Assert.False(testStream.IsDisposed);
    }

    [Fact]
    public async Task ReaderFactory_Stream_Default_Leaves_Stream_Open_Async()
    {
        using var file = File.OpenRead(Path.Combine(TEST_ARCHIVES_PATH, "Zip.deflate.zip"));
        using var testStream = new TestStream(file);

        await using (
            var reader = await ReaderFactory.OpenAsyncReader(new AsyncOnlyStream(testStream))
        )
        {
            await reader.MoveToNextEntryAsync();
        }

        Assert.False(testStream.IsDisposed);
    }

    [Fact]
    public void WriterOptions_Invalid_CompressionLevels_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WriterOptions(CompressionType.Deflate, 10)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WriterOptions(CompressionType.ZStandard, 0)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WriterOptions(CompressionType.BZip2, 1)
        );
    }

    [Fact]
    public void ZipWriterOptions_Invalid_CompressionLevels_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZipWriterOptions(CompressionType.Deflate, 10)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ZipWriterOptions(CompressionType.ZStandard, 23)
        );
    }

    [Fact]
    public void GZipWriterOptions_Invalid_Settings_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GZipWriterOptions(10));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GZipWriterOptions { CompressionType = CompressionType.Deflate }
        );
    }

    [Fact]
    public void ZipWriterEntryOptions_Invalid_CompressionLevel_Throws()
    {
        using var destination = new MemoryStream();
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });
        using var writer = new ZipWriter(
            destination,
            new ZipWriterOptions(CompressionType.Deflate)
        );

        var options = new ZipWriterEntryOptions { CompressionLevel = 11 };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            writer.Write("entry.bin", source, options)
        );
    }

    [Fact]
    public void WriterOptions_Factory_Methods_Create_Valid_Options()
    {
        // ForZip
        var zipOptions = WriterOptions.ForZip();
        Assert.Equal(CompressionType.Deflate, zipOptions.CompressionType);
        Assert.True(zipOptions.LeaveStreamOpen);

        // ForTar
        var tarOptions = WriterOptions.ForTar();
        Assert.Equal(CompressionType.None, tarOptions.CompressionType);

        // ForGZip
        var gzipOptions = WriterOptions.ForGZip();
        Assert.Equal(CompressionType.GZip, gzipOptions.CompressionType);
    }

    [Fact]
    public void WriterOptions_Fluent_Methods_Modify_Correctly()
    {
        var options = WriterOptions.ForZip().WithLeaveStreamOpen(false).WithCompressionLevel(9);

        Assert.Equal(CompressionType.Deflate, options.CompressionType);
        Assert.Equal(9, options.CompressionLevel);
        Assert.False(options.LeaveStreamOpen);
    }

    [Fact]
    public void WriterOptions_Factory_And_Fluent_Equivalent_To_Constructor()
    {
        // Factory + fluent approach
        var factoryApproach = WriterOptions
            .ForZip()
            .WithLeaveStreamOpen(false)
            .WithCompressionLevel(9);

        // Traditional constructor approach
        var constructorApproach = new WriterOptions(CompressionType.Deflate)
        {
            CompressionLevel = 9,
            LeaveStreamOpen = false,
        };

        Assert.Equal(factoryApproach.CompressionType, constructorApproach.CompressionType);
        Assert.Equal(factoryApproach.CompressionLevel, constructorApproach.CompressionLevel);
        Assert.Equal(factoryApproach.LeaveStreamOpen, constructorApproach.LeaveStreamOpen);
    }

    [Fact]
    public void ReaderOptions_Fluent_Methods_Modify_Correctly()
    {
        var options = ReaderOptions
            .ForExternalStream.WithLeaveStreamOpen(false)
            .WithPassword("secret")
            .WithLookForHeader(true)
            .WithBufferSize(65536);

        Assert.False(options.LeaveStreamOpen);
        Assert.Equal("secret", options.Password);
        Assert.True(options.LookForHeader);
        Assert.Equal(65536, options.BufferSize);
    }

    [Fact]
    public void ReaderOptions_Fluent_And_Initializer_Equivalent()
    {
        // Fluent approach
        var fluentApproach = ReaderOptions
            .ForExternalStream.WithLeaveStreamOpen(false)
            .WithPassword("secret")
            .WithLookForHeader(true)
            .WithBufferSize(65536)
            .WithDisableCheckIncomplete(true);

        // Preset + with-expression approach
        var initializerApproach = ReaderOptions.ForExternalStream with
        {
            LeaveStreamOpen = false,
            Password = "secret",
            LookForHeader = true,
            BufferSize = 65536,
            DisableCheckIncomplete = true,
        };

        Assert.Equal(fluentApproach.LeaveStreamOpen, initializerApproach.LeaveStreamOpen);
        Assert.Equal(fluentApproach.Password, initializerApproach.Password);
        Assert.Equal(fluentApproach.LookForHeader, initializerApproach.LookForHeader);
        Assert.Equal(fluentApproach.BufferSize, initializerApproach.BufferSize);
        Assert.Equal(
            fluentApproach.DisableCheckIncomplete,
            initializerApproach.DisableCheckIncomplete
        );
    }

    [Fact]
    public void ReaderOptions_Presets_Have_Correct_Defaults()
    {
        var external = ReaderOptions.ForExternalStream;
        Assert.True(external.LeaveStreamOpen);

        var owned = ReaderOptions.ForFilePath;
        Assert.False(owned.LeaveStreamOpen);
    }

    [Fact]
    public void ExtractionOptions_Presets_Have_Correct_Defaults()
    {
        var safe = ExtractionOptions.SafeExtract;
        Assert.False(safe.Overwrite);

        var flat = ExtractionOptions.FlatExtract;
        Assert.False(flat.ExtractFullPath);
        Assert.True(flat.Overwrite);

        var preserveMetadata = ExtractionOptions.PreserveMetadata;
        Assert.True(preserveMetadata.PreserveFileTime);
        Assert.True(preserveMetadata.PreserveAttributes);

        Assert.Equal(Constants.BufferSize, new ExtractionOptions().BufferSize);
    }

    [Fact]
    public void ArchiveEntry_WriteToFile_Uses_ExtractionOptions_BufferSize()
    {
        using var source = new TrackingReadStream(new byte[16]);
        var entry = new TestArchiveEntry(source);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "buffer-size.txt");

        entry.WriteToFile(destination, new ExtractionOptions { BufferSize = 7 });

        Assert.Equal(7, source.CopyBufferSize);
    }

    [Fact]
    public async Task ArchiveEntry_WriteToFileAsync_Uses_ExtractionOptions_BufferSize()
    {
        using var source = new TrackingReadStream(new byte[16]);
        var entry = new TestArchiveEntry(source);
        var destination = Path.Combine(SCRATCH_FILES_PATH, "buffer-size-async.txt");

        await entry.WriteToFileAsync(destination, new ExtractionOptions { BufferSize = 9 });

        Assert.Equal(9, source.CopyBufferSize);
    }

    [Fact]
    public void Reader_WriteEntryToFile_Uses_ExtractionOptions_BufferSize()
    {
        using var reader = new TrackingReader();
        var destination = Path.Combine(SCRATCH_FILES_PATH, "reader-buffer-size.txt");

        reader.WriteEntryToFile(destination, new ExtractionOptions { BufferSize = 11 });

        Assert.Equal(11, reader.EntryStreamCopyBufferSize);
    }

    [Fact]
    public async Task Reader_WriteEntryToFileAsync_Uses_ExtractionOptions_BufferSize()
    {
        await using var reader = new TrackingReader();
        var destination = Path.Combine(SCRATCH_FILES_PATH, "reader-buffer-size-async.txt");

        await reader.WriteEntryToFileAsync(destination, new ExtractionOptions { BufferSize = 13 });

        Assert.Equal(13, reader.EntryStreamCopyBufferSize);
    }

    [Fact]
    public void Public_Api_Does_Not_Expose_CSharp_9_Required_Metadata()
    {
        var assembly = typeof(ReaderOptions).Assembly;
        const string RequiredMemberAttributeName =
            "System.Runtime.CompilerServices.RequiredMemberAttribute";
        const string SetsRequiredMembersAttributeName =
            "System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute";
        var initOnlyProperties = assembly
            .GetExportedTypes()
            .SelectMany(type =>
                type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.SetMethod?.IsPublic == true)
                    .Where(property =>
                        property
                            .SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                            .Contains(typeof(IsExternalInit))
                    )
                    .Select(property => $"{type.FullName}.{property.Name}")
            )
            .ToArray();

        Assert.Empty(initOnlyProperties);

        var requiredMembers = assembly
            .GetExportedTypes()
            .SelectMany(type =>
                type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                    .Where(member =>
                        member
                            .GetCustomAttributesData()
                            .Any(attribute =>
                                attribute.AttributeType.FullName == RequiredMemberAttributeName
                            )
                    )
                    .Select(member => $"{type.FullName}.{member.Name}")
            )
            .ToArray();

        Assert.Empty(requiredMembers);

        var constructorsWithRequiredMembers = assembly
            .GetExportedTypes()
            .SelectMany(type =>
                type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                    .Where(constructor =>
                        constructor
                            .GetCustomAttributesData()
                            .Any(attribute =>
                                attribute.AttributeType.FullName == SetsRequiredMembersAttributeName
                            )
                    )
                    .Select(_ => $"{type.FullName}.ctor")
            )
            .ToArray();

        Assert.Empty(constructorsWithRequiredMembers);
    }

    [Fact]
    public void ReaderOptions_Factory_ForEncryptedArchive_Sets_Password()
    {
        var options = ReaderOptions.ForEncryptedArchive("myPassword");
        Assert.Equal("myPassword", options.Password);

        var noPassword = ReaderOptions.ForEncryptedArchive();
        Assert.Null(noPassword.Password);
    }

    [Fact]
    public void ReaderOptions_Factory_ForEncoding_Sets_Encoding()
    {
        var encoding = new ArchiveEncoding { Default = System.Text.Encoding.UTF8 };
        var options = ReaderOptions.ForEncoding(encoding);
        Assert.Equal(encoding, options.ArchiveEncoding);
    }

    [Fact]
    public void ReaderOptions_Factory_ForSelfExtractingArchive_Configures_Correctly()
    {
        var options = ReaderOptions.ForSelfExtractingArchive("password");
        Assert.True(options.LookForHeader);
        Assert.Equal("password", options.Password);
        Assert.Equal(1_048_576, options.RewindableBufferSize);

        var noPassword = ReaderOptions.ForSelfExtractingArchive();
        Assert.True(noPassword.LookForHeader);
        Assert.Null(noPassword.Password);
        Assert.Equal(1_048_576, noPassword.RewindableBufferSize);
    }

    private sealed class TrackingReadStream(byte[] data) : MemoryStream(data)
    {
        public int? CopyBufferSize { get; private set; }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            CopyBufferSize = bufferSize;
            base.CopyTo(destination, bufferSize);
        }

        public override Task CopyToAsync(
            Stream destination,
            int bufferSize,
            CancellationToken cancellationToken
        )
        {
            CopyBufferSize = bufferSize;
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }

    private sealed class TestArchiveEntry(Stream source) : IArchiveEntry
    {
        public CompressionType CompressionType => CompressionType.None;
        public DateTime? ArchivedTime => null;
        public long CompressedSize => source.Length;
        public long Crc => 0;
        public DateTime? CreatedTime => null;
        public string? Key => "buffer-size.txt";
        public string? LinkTarget => null;
        public bool IsDirectory => false;
        public bool IsEncrypted => false;
        public bool IsSplitAfter => false;
        public bool IsSolid => false;
        public int VolumeIndexFirst => 0;
        public int VolumeIndexLast => 0;
        public DateTime? LastAccessedTime => null;
        public DateTime? LastModifiedTime => null;
        public long Size => source.Length;
        public int? Attrib => null;
        public SharpCompress.Common.Options.IReaderOptions Options =>
            ReaderOptions.ForExternalStream;
        public bool IsComplete => true;
        public IArchive Archive => throw new NotSupportedException();

        public Stream OpenEntryStream()
        {
            source.Position = 0;
            return source;
        }

        public ValueTask<Stream> OpenEntryStreamAsync(CancellationToken cancellationToken = default)
        {
            source.Position = 0;
            return new ValueTask<Stream>(source);
        }
    }

    private sealed class TrackingReader : IReader, IAsyncReader
    {
        public ArchiveType Type => ArchiveType.Zip;
        public TrackingReadStream Source { get; } = new(new byte[100]);
        public IEntry Entry => new TestArchiveEntry(Source);
        public bool Cancelled => false;
        public int? EntryStreamCopyBufferSize { get; private set; }

        public void Dispose() { }

        public ValueTask DisposeAsync() => default;

        public void WriteEntryTo(Stream writableStream) => throw new NotSupportedException();

        public ValueTask WriteEntryToAsync(
            Stream writableStream,
            CancellationToken cancellationToken = default
        ) => throw new NotSupportedException();

        public void Cancel() { }

        public bool MoveToNextEntry() => false;

        public ValueTask<bool> MoveToNextEntryAsync(
            CancellationToken cancellationToken = default
        ) => new(false);

        public EntryStream OpenEntryStream()
        {
            Source.Position = 0;
            return new TrackingEntryStream(
                this,
                Source,
                bufferSize => EntryStreamCopyBufferSize = bufferSize
            );
        }

        public ValueTask<EntryStream> OpenEntryStreamAsync(
            CancellationToken cancellationToken = default
        ) => new(OpenEntryStream());
    }

    private sealed class TrackingEntryStream(
        IReader reader,
        Stream stream,
        Action<int> copyBufferSize
    ) : EntryStream(reader, stream)
    {
        public override void CopyTo(Stream destination, int bufferSize)
        {
            copyBufferSize(bufferSize);
            base.CopyTo(destination, bufferSize);
        }

        public override Task CopyToAsync(
            Stream destination,
            int bufferSize,
            CancellationToken cancellationToken
        )
        {
            copyBufferSize(bufferSize);
            return base.CopyToAsync(destination, bufferSize, cancellationToken);
        }
    }
}
