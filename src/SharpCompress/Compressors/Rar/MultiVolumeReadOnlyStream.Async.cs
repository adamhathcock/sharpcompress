using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;

namespace SharpCompress.Compressors.Rar;

internal sealed partial class MultiVolumeReadOnlyStream : MultiVolumeReadOnlyStreamBase
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var totalRead = 0;
        var currentOffset = offset;
        var currentCount = count;
        while (currentCount > 0)
        {
            var readSize = currentCount;
            if (currentCount > maxPosition - currentPosition)
            {
                readSize = (int)(maxPosition - currentPosition);
            }

            var read = await currentStream
                .NotNull()
                .ReadAsync(buffer, currentOffset, readSize, cancellationToken)
                .ConfigureAwait(false);
            if (read < 0)
            {
                throw new IncompleteArchiveException("Unexpected end of stream.");
            }

            currentPosition += read;
            currentOffset += read;
            currentCount -= read;
            totalRead += read;
            if (
                ((maxPosition - currentPosition) == 0)
                && filePartEnumerator.Current.FileHeader.IsSplitAfter
            )
            {
                if (filePartEnumerator.Current.FileHeader.R4Salt != null)
                {
                    throw new InvalidFormatException(
                        "Sharpcompress currently does not support multi-volume decryption."
                    );
                }

                var fileName = filePartEnumerator.Current.FileHeader.FileName;
                if (!filePartEnumerator.MoveNext())
                {
                    throw new InvalidFormatException(
                        "Multi-part rar file is incomplete.  Entry expects a new volume: "
                            + fileName
                    );
                }

                InitializeNextFilePart();
            }
            else
            {
                break;
            }
        }

        return totalRead;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var totalRead = 0;
        var currentOffset = 0;
        var currentCount = buffer.Length;
        while (currentCount > 0)
        {
            var readSize = currentCount;
            if (currentCount > maxPosition - currentPosition)
            {
                readSize = (int)(maxPosition - currentPosition);
            }

            var read = await currentStream
                .NotNull()
                .ReadAsync(buffer.Slice(currentOffset, readSize), cancellationToken)
                .ConfigureAwait(false);
            if (read < 0)
            {
                throw new IncompleteArchiveException("Unexpected end of stream.");
            }

            currentPosition += read;
            currentOffset += read;
            currentCount -= read;
            totalRead += read;
            if (
                ((maxPosition - currentPosition) == 0)
                && filePartEnumerator.Current.FileHeader.IsSplitAfter
            )
            {
                if (filePartEnumerator.Current.FileHeader.R4Salt != null)
                {
                    throw new InvalidFormatException(
                        "Sharpcompress currently does not support multi-volume decryption."
                    );
                }
                var fileName = filePartEnumerator.Current.FileHeader.FileName;
                if (!filePartEnumerator.MoveNext())
                {
                    throw new InvalidFormatException(
                        "Multi-part rar file is incomplete.  Entry expects a new volume: "
                            + fileName
                    );
                }
                InitializeNextFilePart();
            }
            else
            {
                break;
            }
        }
        return totalRead;
    }
#endif
}
