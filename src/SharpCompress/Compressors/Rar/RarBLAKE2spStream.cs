using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar;

internal class RarBLAKE2spStream : RarStream
{
    private readonly MultiVolumeReadOnlyStream readStream;
    private readonly bool disableCRCCheck;

    const uint BLAKE2S_NUM_ROUNDS = 10;
    const uint BLAKE2S_FINAL_FLAG = (~(uint)0);
    const int BLAKE2S_BLOCK_SIZE = 64;
    const int BLAKE2S_DIGEST_SIZE = 32;
    const int BLAKE2SP_PARALLEL_DEGREE = 8;
    const uint BLAKE2S_INIT_IV_SIZE = 8;

    static readonly UInt32[] k_BLAKE2S_IV =
    {
        0x6A09E667U,
        0xBB67AE85U,
        0x3C6EF372U,
        0xA54FF53AU,
        0x510E527FU,
        0x9B05688CU,
        0x1F83D9ABU,
        0x5BE0CD19U,
    };

    static readonly byte[][] k_BLAKE2S_Sigma =
    {
        new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
        new byte[] { 14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3 },
        new byte[] { 11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4 },
        new byte[] { 7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8 },
        new byte[] { 9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13 },
        new byte[] { 2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9 },
        new byte[] { 12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11 },
        new byte[] { 13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10 },
        new byte[] { 6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5 },
        new byte[] { 10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0 },
    };

    internal class BLAKE2S
    {
        internal UInt32[] h;
        internal UInt32[] t;
        internal UInt32[] f;
        internal byte[] b;
        internal int bufferPosition;
        internal UInt32 lastNodeFlag;
        UInt32[] dummy;

        public BLAKE2S()
        {
            h = new uint[BLAKE2S_INIT_IV_SIZE];
            t = new uint[2];
            f = new uint[2];
            b = new byte[BLAKE2S_BLOCK_SIZE];
            dummy = new uint[2];
        }
    };

    internal class BLAKE2SP
    {
        internal BLAKE2S[] S;
        internal int bufferPosition;

        public BLAKE2SP()
        {
            S = new BLAKE2S[BLAKE2SP_PARALLEL_DEGREE];
            for (var i = 0; i < S.Length; i++)
            {
                S[i] = new BLAKE2S();
            }
        }
    };

    BLAKE2SP _blake2sp;

    byte[] _hash = { };

    public RarBLAKE2spStream(
        IRarUnpack unpack,
        FileHeader fileHeader,
        MultiVolumeReadOnlyStream readStream
    )
        : base(unpack, fileHeader, readStream)
    {
        this.readStream = readStream;
        disableCRCCheck = fileHeader.IsEncrypted;
        _hash = fileHeader.FileCrc;
        _blake2sp = new BLAKE2SP();
        ResetCrc();
    }

    public byte[] GetCrc() => _hash;

    internal void ResetCrc(BLAKE2S hash)
    {
        for (UInt32 j = 0; j < BLAKE2S_INIT_IV_SIZE; j++)
        {
            hash.h[j] = k_BLAKE2S_IV[j];
        }
        hash.t[0] = 0;
        hash.t[1] = 0;
        hash.f[0] = 0;
        hash.f[1] = 0;
        hash.bufferPosition = 0;
        hash.lastNodeFlag = 0;
    }

    internal void G(
        ref UInt32[] m,
        ref byte[] sigma,
        int i,
        ref UInt32 a,
        ref UInt32 b,
        ref UInt32 c,
        ref UInt32 d
    )
    {
        a += b + m[sigma[2 * i]];
        d ^= a;
        d = (d >> 16) | (d << 16);
        c += d;
        b ^= c;
        b = (b >> 12) | (b << 20);

        a += b + m[sigma[2 * i + 1]];
        d ^= a;
        d = (d >> 8) | (d << 24);
        c += d;
        b ^= c;
        b = (b >> 7) | (b << 25);
    }

    internal void Compress(BLAKE2S hash)
    {
        var m = new UInt32[16];
        var v = new UInt32[16];

        for (var i = 0; i < 16; i++)
        {
            m[i] = BitConverter.ToUInt32(hash.b, i * 4);
        }

        for (var i = 0; i < 8; i++)
        {
            v[i] = hash.h[i];
        }

        v[8] = k_BLAKE2S_IV[0];
        v[9] = k_BLAKE2S_IV[1];
        v[10] = k_BLAKE2S_IV[2];
        v[11] = k_BLAKE2S_IV[3];

        v[12] = hash.t[0] ^ k_BLAKE2S_IV[4];
        v[13] = hash.t[1] ^ k_BLAKE2S_IV[5];
        v[14] = hash.f[0] ^ k_BLAKE2S_IV[6];
        v[15] = hash.f[1] ^ k_BLAKE2S_IV[7];

        for (var r = 0; r < BLAKE2S_NUM_ROUNDS; r++)
        {
            ref byte[] sigma = ref k_BLAKE2S_Sigma[r];

            G(ref m, ref sigma, 0, ref v[0], ref v[4], ref v[8], ref v[12]);
            G(ref m, ref sigma, 1, ref v[1], ref v[5], ref v[9], ref v[13]);
            G(ref m, ref sigma, 2, ref v[2], ref v[6], ref v[10], ref v[14]);
            G(ref m, ref sigma, 3, ref v[3], ref v[7], ref v[11], ref v[15]);
            G(ref m, ref sigma, 4, ref v[0], ref v[5], ref v[10], ref v[15]);
            G(ref m, ref sigma, 5, ref v[1], ref v[6], ref v[11], ref v[12]);
            G(ref m, ref sigma, 6, ref v[2], ref v[7], ref v[8], ref v[13]);
            G(ref m, ref sigma, 7, ref v[3], ref v[4], ref v[9], ref v[14]);
        }

        for (var i = 0; i < 8; i++)
        {
            hash.h[i] ^= v[i] ^ v[i + 8];
        }
    }

    internal void Update(BLAKE2S hash, ReadOnlySpan<byte> data, int size)
    {
        var i = 0;
        while (size != 0)
        {
            var pos = hash.bufferPosition;
            var reminder = BLAKE2S_BLOCK_SIZE - pos;

            if (size <= reminder)
            {
                data.Slice(i, size).CopyTo(new Span<byte>(hash.b, pos, size));
                hash.bufferPosition += size;
                return;
            }
            data.Slice(i, reminder).CopyTo(new Span<byte>(hash.b, pos, reminder));
            hash.t[0] += BLAKE2S_BLOCK_SIZE;
            hash.t[1] += hash.t[0] < BLAKE2S_BLOCK_SIZE ? 1U : 0U;
            Compress(hash);
            hash.bufferPosition = 0;
            i += reminder;
            size -= reminder;
        }
    }

    internal byte[] Final(BLAKE2S hash)
    {
        hash.t[0] += (uint)hash.bufferPosition;
        hash.t[1] += hash.t[0] < hash.bufferPosition ? 1U : 0U;
        hash.f[0] = BLAKE2S_FINAL_FLAG;
        hash.f[1] = hash.lastNodeFlag;
        Array.Clear(hash.b, hash.bufferPosition, BLAKE2S_BLOCK_SIZE - hash.bufferPosition);
        Compress(hash);

        var mem = new MemoryStream();

        for (var i = 0; i < 8; i++)
        {
            mem.Write(BitConverter.GetBytes(hash.h[i]), 0, 4);
        }

        return mem.ToArray();
    }

    public void ResetCrc()
    {
        _blake2sp.bufferPosition = 0;

        for (UInt32 i = 0; i < BLAKE2SP_PARALLEL_DEGREE; i++)
        {
            _blake2sp.S[i].bufferPosition = 0;
            ResetCrc(_blake2sp.S[i]);
            _blake2sp.S[i].h[0] ^= (BLAKE2S_DIGEST_SIZE | BLAKE2SP_PARALLEL_DEGREE << 16 | 2 << 24);
            _blake2sp.S[i].h[2] ^= i;
            _blake2sp.S[i].h[3] ^= (BLAKE2S_DIGEST_SIZE << 24);
        }

        _blake2sp.S[BLAKE2SP_PARALLEL_DEGREE - 1].lastNodeFlag = BLAKE2S_FINAL_FLAG;
    }

    internal void Update(BLAKE2SP hash, ReadOnlySpan<byte> data, int size)
    {
        var i = 0;
        var pos = hash.bufferPosition;
        while (size != 0)
        {
            var index = pos / BLAKE2S_BLOCK_SIZE;
            var reminder = BLAKE2S_BLOCK_SIZE - (pos & (BLAKE2S_BLOCK_SIZE - 1));
            if (reminder > size)
            {
                reminder = size;
            }
            //            Update(hash.S[index], data, size);
            Update(hash.S[index], data.Slice(i, reminder), reminder);
            size -= reminder;
            i += reminder;
            pos += reminder;
            pos &= (BLAKE2S_BLOCK_SIZE * (BLAKE2SP_PARALLEL_DEGREE - 1));
        }
        hash.bufferPosition = pos;
    }

    internal byte[] Final(BLAKE2SP hash)
    {
        var h = new BLAKE2S();

        ResetCrc(h);
        h.h[0] ^= (BLAKE2S_DIGEST_SIZE | BLAKE2SP_PARALLEL_DEGREE << 16 | 2 << 24);
        h.h[3] ^= (1 << 16 | BLAKE2S_DIGEST_SIZE << 24);
        h.lastNodeFlag = BLAKE2S_FINAL_FLAG;

        for (var i = 0; i < BLAKE2SP_PARALLEL_DEGREE; i++)
        {
            var digest = Final(_blake2sp.S[i]);
            Update(h, digest, BLAKE2S_DIGEST_SIZE);
        }

        return Final(h);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var result = base.Read(buffer, offset, count);
        if (result != 0)
        {
            Update(_blake2sp, new ReadOnlySpan<byte>(buffer, offset, result), result);
        }
        else
        {
            _hash = Final(_blake2sp);
            if (!disableCRCCheck && !(GetCrc().SequenceEqual(readStream.CurrentCrc)) && count != 0)
            {
                // NOTE: we use the last FileHeader in a multipart volume to check CRC
                throw new InvalidFormatException("file crc mismatch");
            }
        }

        return result;
    }
}
