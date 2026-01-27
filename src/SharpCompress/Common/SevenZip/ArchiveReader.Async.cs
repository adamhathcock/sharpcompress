#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip;

internal sealed partial class ArchiveReader
{
    public async ValueTask OpenAsync(
        Stream stream,
        bool lookForHeader,
        CancellationToken cancellationToken = default
    )
    {
        Close();

        _streamOrigin = stream.Position;
        _streamEnding = stream.Length;

        var canScan = lookForHeader ? 0x80000 - 20 : 0;
        while (true)
        {
            // TODO: Check Signature!
            _header = new byte[0x20];
            await stream.ReadExactAsync(_header, 0, 0x20, cancellationToken);

            if (
                !lookForHeader
                || _header
                    .AsSpan(0, length: 6)
                    .SequenceEqual<byte>([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C])
            )
            {
                break;
            }

            if (canScan == 0)
            {
                throw new InvalidFormatException("Unable to find 7z signature");
            }

            canScan--;
            stream.Position = ++_streamOrigin;
        }

        _stream = stream;
    }

    public async ValueTask<ArchiveDatabase> ReadDatabaseAsync(
        IPasswordProvider pass,
        CancellationToken cancellationToken = default
    )
    {
        var db = new ArchiveDatabase(pass);
        db.Clear();

        db._majorVersion = _header[6];
        db._minorVersion = _header[7];

        if (db._majorVersion != 0)
        {
            throw new InvalidOperationException();
        }

        var crcFromArchive = DataReader.Get32(_header, 8);
        var nextHeaderOffset = (long)DataReader.Get64(_header, 0xC);
        var nextHeaderSize = (long)DataReader.Get64(_header, 0x14);
        var nextHeaderCrc = DataReader.Get32(_header, 0x1C);

        var crc = Crc.INIT_CRC;
        crc = Crc.Update(crc, nextHeaderOffset);
        crc = Crc.Update(crc, nextHeaderSize);
        crc = Crc.Update(crc, nextHeaderCrc);
        crc = Crc.Finish(crc);

        if (crc != crcFromArchive)
        {
            throw new InvalidOperationException();
        }

        db._startPositionAfterHeader = _streamOrigin + 0x20;

        // empty header is ok
        if (nextHeaderSize == 0)
        {
            db.Fill();
            return db;
        }

        if (nextHeaderOffset < 0 || nextHeaderSize < 0 || nextHeaderSize > int.MaxValue)
        {
            throw new InvalidOperationException();
        }

        if (nextHeaderOffset > _streamEnding - db._startPositionAfterHeader)
        {
            throw new InvalidOperationException("nextHeaderOffset is invalid");
        }

        _stream.Seek(nextHeaderOffset, SeekOrigin.Current);

        var header = new byte[nextHeaderSize];
        await _stream.ReadExactAsync(header, 0, header.Length, cancellationToken);

        if (Crc.Finish(Crc.Update(Crc.INIT_CRC, header, 0, header.Length)) != nextHeaderCrc)
        {
            throw new InvalidOperationException();
        }

        using (var streamSwitch = new CStreamSwitch())
        {
            streamSwitch.Set(this, header);

            var type = ReadId();
            if (type != BlockType.Header)
            {
                if (type != BlockType.EncodedHeader)
                {
                    throw new InvalidOperationException();
                }

                var dataVector = ReadAndDecodePackedStreams(
                    db._startPositionAfterHeader,
                    db.PasswordProvider
                );

                // compressed header without content is odd but ok
                if (dataVector.Count == 0)
                {
                    db.Fill();
                    return db;
                }

                if (dataVector.Count != 1)
                {
                    throw new InvalidOperationException();
                }

                streamSwitch.Set(this, dataVector[0]);

                if (ReadId() != BlockType.Header)
                {
                    throw new InvalidOperationException();
                }
            }

            ReadHeader(db, db.PasswordProvider);
        }
        db.Fill();
        return db;
    }
}
