using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Compressors.RLE90;
using SharpCompress.Compressors.Squeezed;
using SharpCompress.IO;

public partial class ArcLzwStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => _stream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    private Stream _stream;
    private bool _processed;
    private bool _useCrunched;
    private int _compressedSize;

    private const int BITS = 12;
    private const int CRUNCH_BITS = 12;
    private const int SQUASH_BITS = 13;
    private const int INIT_BITS = 9;
    private const ushort FIRST = 257;
    private const ushort CLEAR = 256;

    private ushort oldcode;
    private byte finchar;
    private int n_bits;
    private ushort maxcode;
    private ushort[] prefix = new ushort[8191];
    private byte[] suffix = new byte[8191];
    private bool clearFlag;
    private Stack<byte> stack = new Stack<byte>();
    private ushort freeEnt;
    private ushort maxcodemax;

    public ArcLzwStream(Stream stream, int compressedSize, bool useCrunched = true)
    {
        _stream = stream;
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(ArcLzwStream));
#endif
        _useCrunched = useCrunched;
        _compressedSize = compressedSize;

        oldcode = 0;
        finchar = 0;
        n_bits = 0;
        maxcode = 0;
        clearFlag = false;
        freeEnt = FIRST;
        maxcodemax = 0;
    }

    private ushort? GetCode(BitReader reader)
    {
        if (clearFlag || freeEnt > maxcode)
        {
            if (freeEnt > maxcode)
            {
                n_bits++;
                maxcode = (n_bits == BITS) ? maxcodemax : (ushort)((1 << n_bits) - 1);
            }
            if (clearFlag)
            {
                clearFlag = false;
                n_bits = INIT_BITS;
                maxcode = (ushort)((1 << n_bits) - 1);
            }
        }
        return (ushort?)reader.ReadBits(n_bits);
    }

    public List<byte> Decompress(byte[] input, bool useCrunched)
    {
        var result = new List<byte>();
        int bits = useCrunched ? CRUNCH_BITS : SQUASH_BITS;

        if (useCrunched)
        {
            if (input[0] != BITS)
            {
                throw new InvalidDataException($"File packed with {input[0]}, expected {BITS}.");
            }

            input = input.Skip(1).ToArray();
        }

        maxcodemax = (ushort)(1 << bits);
        clearFlag = false;
        n_bits = INIT_BITS;
        maxcode = (ushort)((1 << n_bits) - 1);

        for (int i = 0; i < 256; i++)
        {
            suffix[i] = (byte)i;
        }

        var reader = new BitReader(input);
        freeEnt = FIRST;

        if (GetCode(reader) is ushort old)
        {
            oldcode = old;
            finchar = (byte)oldcode;
            result.Add(finchar);
        }

        while (GetCode(reader) is ushort code)
        {
            if (code == CLEAR)
            {
                Array.Clear(prefix, 0, prefix.Length);
                clearFlag = true;
                freeEnt = (ushort)(FIRST - 1);

                if (GetCode(reader) is ushort c)
                {
                    code = c;
                }
                else
                {
                    break;
                }
            }

            ushort incode = code;

            if (code >= freeEnt)
            {
                stack.Push(finchar);
                code = oldcode;
            }

            while (code >= 256)
            {
                stack.Push(suffix[code]);
                code = prefix[code];
            }

            finchar = suffix[code];
            stack.Push(finchar);

            while (stack.Count > 0)
            {
                result.Add(stack.Pop());
            }
            code = freeEnt;
            if (code < maxcodemax)
            {
                prefix[code] = oldcode;
                suffix[code] = finchar;
                freeEnt = (ushort)(code + 1);
            }

            oldcode = incode;
        }

        return result;
    }

    // Stream base class implementation
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotImplementedException();
    public override long Position
    {
        get => _stream.Position;
        set => throw new NotImplementedException();
    }

    public override void Flush() => throw new NotImplementedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_processed)
        {
            return 0;
        }
        _processed = true;
        var data = new byte[_compressedSize];
        _stream.Read(data, 0, _compressedSize);
        var decoded = Decompress(data, _useCrunched);
        var result = decoded.Count();
        if (_useCrunched)
        {
            var unpacked = RLE.UnpackRLE(decoded.ToArray());
            unpacked.CopyTo(buffer, 0);
            result = unpacked.Count;
        }
        else
        {
            decoded.CopyTo(buffer, 0);
        }
        return result;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotImplementedException();

    public override void SetLength(long value) => throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotImplementedException();

    protected override void Dispose(bool disposing)
    {
#if DEBUG_STREAMS
        this.DebugDispose(typeof(ArcLzwStream));
#endif
        base.Dispose(disposing);
    }
}
