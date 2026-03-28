using System;
using System.Runtime.InteropServices;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar;

internal partial class RarBLAKE2spStream : RarStream
{
    private readonly MultiVolumeReadOnlyStreamBase readStream;
    private readonly bool disableCRCCheck;

    private const int BLAKE2S_NUM_ROUNDS = 10;
    private const uint BLAKE2S_FINAL_FLAG = ~(uint)0;
    private const int BLAKE2S_BLOCK_SIZE = 64;
    private const int BLAKE2S_DIGEST_SIZE = 32;
    private const int BLAKE2SP_PARALLEL_DEGREE = 8;
    private const int BLAKE2S_INIT_IV_SIZE = 8;

    private static readonly uint[] k_BLAKE2S_IV =
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

    private static readonly byte[][] k_BLAKE2S_Sigma =
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

    private sealed class BLAKE2S
    {
        internal readonly uint[] h;
        internal readonly uint[] t;
        internal readonly uint[] f;
        internal readonly byte[] b;
        internal int bufferPosition;
        internal uint lastNodeFlag;

        public BLAKE2S()
        {
            h = new uint[BLAKE2S_INIT_IV_SIZE];
            t = new uint[2];
            f = new uint[2];
            b = new byte[BLAKE2S_BLOCK_SIZE];
        }
    };

    private sealed class BLAKE2SP
    {
        internal readonly BLAKE2S[] S;
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

    private BLAKE2SP? _blake2sp;
    private byte[]? _hash;

    private RarBLAKE2spStream(
        IRarUnpack unpack,
        FileHeader fileHeader,
        MultiVolumeReadOnlyStreamBase readStream
    )
        : base(unpack, fileHeader, readStream)
    {
        this.readStream = readStream;

        // TODO: rar uses a modified hash xor'ed with encryption key?
        disableCRCCheck = fileHeader.IsEncrypted;
        this._blake2sp = CreateBlake2sp();
    }

    public static RarBLAKE2spStream Create(
        IRarUnpack unpack,
        FileHeader fileHeader,
        MultiVolumeReadOnlyStream readStream
    )
    {
        var stream = new RarBLAKE2spStream(unpack, fileHeader, readStream);
        stream.Initialize();
        return stream;
    }

    // Async methods moved to RarBLAKE2spStream.Async.cs

    public byte[] GetCrc() =>
        this._hash
        ?? throw new InvalidOperationException(
            "hash not computed, has the stream been fully drained?"
        );

    private static void ResetCrc(BLAKE2S hash)
    {
        k_BLAKE2S_IV.AsSpan().CopyTo(hash.h);
        hash.t[0] = 0;
        hash.t[1] = 0;
        hash.f[0] = 0;
        hash.f[1] = 0;
        hash.bufferPosition = 0;
        hash.lastNodeFlag = 0;
    }

    private static void G(
        Span<uint> m,
        byte[] sigma,
        int i,
        ref uint a,
        ref uint b,
        ref uint c,
        ref uint d
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

    private static void Compress(BLAKE2S hash)
    {
        Span<uint> m = stackalloc uint[16];
        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.Cast<byte, uint>(hash.b).CopyTo(m);
        }
        else
        {
            for (var i = 0; i < 16; i++)
            {
                m[i] = BitConverter.ToUInt32(hash.b, i * 4);
            }
        }

        Span<uint> v = stackalloc uint[16];
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
            var sigma = k_BLAKE2S_Sigma[r];
            G(m, sigma, 0, ref v[0], ref v[4], ref v[8], ref v[12]);
            G(m, sigma, 1, ref v[1], ref v[5], ref v[9], ref v[13]);
            G(m, sigma, 2, ref v[2], ref v[6], ref v[10], ref v[14]);
            G(m, sigma, 3, ref v[3], ref v[7], ref v[11], ref v[15]);
            G(m, sigma, 4, ref v[0], ref v[5], ref v[10], ref v[15]);
            G(m, sigma, 5, ref v[1], ref v[6], ref v[11], ref v[12]);
            G(m, sigma, 6, ref v[2], ref v[7], ref v[8], ref v[13]);
            G(m, sigma, 7, ref v[3], ref v[4], ref v[9], ref v[14]);
        }

        for (var i = 0; i < 8; i++)
        {
            hash.h[i] ^= v[i] ^ v[i + 8];
        }
    }

    private static void Update(BLAKE2S hash, ReadOnlySpan<byte> data)
    {
        while (data.Length != 0)
        {
            var pos = hash.bufferPosition;
            var chunkSize = BLAKE2S_BLOCK_SIZE - pos;
            if (data.Length <= chunkSize)
            {
                data.CopyTo(hash.b.AsSpan(pos));
                hash.bufferPosition += data.Length;
                return;
            }
            data.Slice(0, chunkSize).CopyTo(hash.b.AsSpan(pos));
            hash.t[0] += BLAKE2S_BLOCK_SIZE;
            hash.t[1] += hash.t[0] < BLAKE2S_BLOCK_SIZE ? 1U : 0U;
            Compress(hash);
            hash.bufferPosition = 0;
            data = data.Slice(chunkSize);
        }
    }

    private static void Final(BLAKE2S hash, Span<byte> output)
    {
        hash.t[0] += (uint)hash.bufferPosition;
        hash.t[1] += hash.t[0] < hash.bufferPosition ? 1U : 0U;
        hash.f[0] = BLAKE2S_FINAL_FLAG;
        hash.f[1] = hash.lastNodeFlag;
        hash.b.AsSpan(hash.bufferPosition).Clear();
        Compress(hash);

        if (BitConverter.IsLittleEndian)
        {
            MemoryMarshal.Cast<uint, byte>(hash.h).CopyTo(output);
        }
        else
        {
            for (var i = 0; i < 8; i++)
            {
                var v = hash.h[i];
                output[i * 4] = (byte)v;
                output[i * 4 + 1] = (byte)(v >> 8);
                output[i * 4 + 2] = (byte)(v >> 16);
                output[i * 4 + 3] = (byte)(v >> 24);
            }
        }
    }

    private static BLAKE2SP CreateBlake2sp()
    {
        var blake2sp = new BLAKE2SP();

        for (var i = 0; i < BLAKE2SP_PARALLEL_DEGREE; i++)
        {
            var blake2S = blake2sp.S[i];
            ResetCrc(blake2S);

            var h = blake2S.h;
            // word[0]: digest_length | (fanout<<16) | (depth<<24)
            h[0] ^= BLAKE2S_DIGEST_SIZE | (BLAKE2SP_PARALLEL_DEGREE << 16) | (2 << 24);
            // word[2]: node_offset = leaf index
            h[2] ^= (uint)i;
            // word[3]: inner_length in bits 24-31
            h[3] ^= BLAKE2S_DIGEST_SIZE << 24;
        }

        blake2sp.S[BLAKE2SP_PARALLEL_DEGREE - 1].lastNodeFlag = BLAKE2S_FINAL_FLAG;
        return blake2sp;
    }

    private static void Update(BLAKE2SP hash, ReadOnlySpan<byte> data)
    {
        var pos = hash.bufferPosition;
        while (data.Length != 0)
        {
            var index = pos / BLAKE2S_BLOCK_SIZE;
            var chunkSize = BLAKE2S_BLOCK_SIZE - (pos & (BLAKE2S_BLOCK_SIZE - 1));
            if (chunkSize > data.Length)
            {
                chunkSize = data.Length;
            }
            Update(hash.S[index], data.Slice(0, chunkSize));
            data = data.Slice(chunkSize);
            pos = (pos + chunkSize) & (BLAKE2S_BLOCK_SIZE * BLAKE2SP_PARALLEL_DEGREE - 1);
        }
        hash.bufferPosition = pos;
    }

    private static byte[] Final(BLAKE2SP blake2sp)
    {
        var blake2s = new BLAKE2S();
        ResetCrc(blake2s);

        var h = blake2s.h;
        // word[0]: digest_length | (fanout<<16) | (depth<<24)  — same as leaves
        h[0] ^= BLAKE2S_DIGEST_SIZE | (BLAKE2SP_PARALLEL_DEGREE << 16) | (2 << 24);
        // word[3]: node_depth=1 (bits 16-23), inner_length=32 (bits 24-31)
        h[3] ^= (1 << 16) | (BLAKE2S_DIGEST_SIZE << 24);
        blake2s.lastNodeFlag = BLAKE2S_FINAL_FLAG;

        Span<byte> digest = stackalloc byte[BLAKE2S_DIGEST_SIZE];
        for (var i = 0; i < BLAKE2SP_PARALLEL_DEGREE; i++)
        {
            Final(blake2sp.S[i], digest);
            Update(blake2s, digest);
        }

        Final(blake2s, digest);
        return digest.ToArray();
    }

    private void EnsureHash()
    {
        if (this._hash == null)
        {
            this._hash = Final(this._blake2sp!);
            // prevent incorrect usage past hash finality by failing fast
            this._blake2sp = null;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var result = base.Read(buffer, offset, count);
        if (result != 0)
        {
            Update(this._blake2sp!, new ReadOnlySpan<byte>(buffer, offset, result));
        }
        else
        {
            EnsureHash();
            if (!disableCRCCheck && !GetCrc().SequenceEqual(readStream.CurrentCrc) && count != 0)
            {
                // NOTE: we use the last FileHeader in a multipart volume to check CRC
                throw new InvalidFormatException("file crc mismatch");
            }
        }

        return result;
    }
}
