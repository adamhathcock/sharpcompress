#nullable disable

using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Xz;

[CLSCompliant(false)]
public sealed class XZStream : XZReadOnlyStream
{
    public static bool IsXZStream(Stream stream)
    {
        try
        {
            return null != XZHeader.FromStream(stream);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void AssertBlockCheckTypeIsSupported()
    {
        switch (Header.BlockCheckType)
        {
            case CheckType.NONE:
            case CheckType.CRC32:
            case CheckType.CRC64:
            case CheckType.SHA256:
                break;
            default:
                throw new InvalidFormatException("Check Type unknown to this version of decoder.");
        }
    }

    public XZHeader Header { get; private set; }
    public XZIndex Index { get; private set; }
    public XZFooter Footer { get; private set; }
    public bool HeaderIsRead { get; private set; }
    private XZBlock _currentBlock;

    private bool _endOfStream;

    public XZStream(Stream stream)
        : base(stream) { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = 0;
        if (_endOfStream)
        {
            return bytesRead;
        }

        if (!HeaderIsRead)
        {
            ReadHeader();
        }

        bytesRead = ReadBlocks(buffer, offset, count);
        if (bytesRead < count)
        {
            _endOfStream = true;
            ReadIndex();
            ReadFooter();
        }
        return bytesRead;
    }

    private void ReadHeader()
    {
        Header = XZHeader.FromStream(BaseStream);
        AssertBlockCheckTypeIsSupported();
        HeaderIsRead = true;
    }

    private void ReadIndex() => Index = XZIndex.FromStream(BaseStream, true);

    // TODO veryfy Index
    private void ReadFooter() => Footer = XZFooter.FromStream(BaseStream);

    // TODO verify footer
    private int ReadBlocks(byte[] buffer, int offset, int count)
    {
        var bytesRead = 0;
        if (_currentBlock is null)
        {
            NextBlock();
        }

        for (; ; )
        {
            try
            {
                if (bytesRead >= count)
                {
                    break;
                }

                var remaining = count - bytesRead;
                var newOffset = offset + bytesRead;
                var justRead = _currentBlock.Read(buffer, newOffset, remaining);
                if (justRead < remaining)
                {
                    NextBlock();
                }

                bytesRead += justRead;
            }
            catch (XZIndexMarkerReachedException)
            {
                break;
            }
        }
        return bytesRead;
    }

    private void NextBlock() =>
        _currentBlock = new XZBlock(BaseStream, Header.BlockCheckType, Header.BlockCheckSize);
}
