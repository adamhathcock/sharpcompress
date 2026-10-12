using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.LZMA;

// AES has no password verifier. Decoder errors can mean either a wrong password
// or damaged ciphertext; retain the codec error without claiming certainty.
internal sealed class EncryptedDecoderStream(Stream decoder) : DecoderStream2
{
    internal static CryptographicException DecodingException(Exception exception) =>
        new("Incorrect password or corrupt encrypted 7z data.", exception);

    public override long Length => decoder.Length;
    public override long Position => decoder.Position;

    public override int Read(byte[] buffer, int offset, int count)
    {
        try
        {
            return decoder.Read(buffer, offset, count);
        }
        catch (DataErrorException ex)
        {
            throw DecodingException(ex);
        }
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await decoder
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DataErrorException ex)
        {
            throw DecodingException(ex);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            decoder.Dispose();
        }
        base.Dispose(disposing);
    }
}
