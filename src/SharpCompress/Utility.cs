using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress;

internal static partial class Utility
{
    public static bool UseSyncOverAsyncDispose()
    {
        var useSyncOverAsync = false;
#if LEGACY_DOTNET
        useSyncOverAsync = true;
#endif
        return useSyncOverAsync;
    }

    private static readonly HashSet<char> invalidChars = new(Path.GetInvalidFileNameChars());

    public static ReadOnlyCollection<T> ToReadOnly<T>(this IList<T> items) => new(items);

    /// <summary>
    /// Performs an unsigned bitwise right shift with the specified number
    /// </summary>
    /// <param name="number">Number to operate on</param>
    /// <param name="bits">Amount of bits to shift</param>
    /// <returns>The resulting number from the shift operation</returns>
    public static int URShift(int number, int bits) => (int)((uint)number >> bits);

    /// <summary>
    /// Performs an unsigned bitwise right shift with the specified number
    /// </summary>
    /// <param name="number">Number to operate on</param>
    /// <param name="bits">Amount of bits to shift</param>
    /// <returns>The resulting number from the shift operation</returns>
    public static long URShift(long number, int bits) => (long)((ulong)number >> bits);

    public static void SetSize(this List<byte> list, int count)
    {
        if (count > list.Count)
        {
            // Ensure the list only needs to grow once
            list.Capacity = count;
            for (var i = list.Count; i < count; i++)
            {
                list.Add(0x0);
            }
        }
        else
        {
            list.RemoveRange(count, list.Count - count);
        }
    }

    public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
    {
        foreach (var item in items)
        {
            action(item);
        }
    }

    public static IEnumerable<T> AsEnumerable<T>(this T item)
    {
        yield return item;
    }

    public static DateTime DosDateToDateTime(ushort iDate, ushort iTime)
    {
        var year = (iDate / 512) + 1980;
        var month = iDate % 512 / 32;
        var day = iDate % 512 % 32;
        var hour = iTime / 2048;
        var minute = iTime % 2048 / 32;
        var second = iTime % 2048 % 32 * 2;

        if (iDate == ushort.MaxValue || month == 0 || day == 0)
        {
            year = 1980;
            month = 1;
            day = 1;
        }

        if (iTime == ushort.MaxValue)
        {
            hour = minute = second = 0;
        }

        DateTime dt;
        try
        {
            dt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
        }
        catch
        {
            dt = new DateTime();
        }
        return dt;
    }

    public static uint DateTimeToDosTime(this DateTime? dateTime)
    {
        if (dateTime is null)
        {
            return 0;
        }

        var localDateTime = dateTime.Value.ToLocalTime();

        return (uint)(
            (localDateTime.Second / 2)
            | (localDateTime.Minute << 5)
            | (localDateTime.Hour << 11)
            | (localDateTime.Day << 16)
            | (localDateTime.Month << 21)
            | ((localDateTime.Year - 1980) << 25)
        );
    }

    public static DateTime DosDateToDateTime(uint iTime) =>
        DosDateToDateTime((ushort)(iTime / 65536), (ushort)(iTime % 65536));

    /// <summary>
    /// Convert Unix time value to a DateTime object.
    /// </summary>
    /// <param name="unixtime">The Unix time stamp you want to convert to DateTime.</param>
    /// <returns>Returns a DateTime object that represents value of the Unix time.</returns>
    public static DateTime UnixTimeToDateTime(long unixtime)
    {
        var sTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return sTime.AddSeconds(unixtime);
    }

    extension(Stream source)
    {
        public long TransferTo(Stream destination, long maxLength)
        {
            // Use ReadOnlySubStream to limit reading and leverage framework's CopyTo
            using var limitedStream = new IO.ReadOnlySubStream(source, maxLength);
            limitedStream.CopyTo(destination, Constants.BufferSize);
            return limitedStream.Position;
        }

        public async ValueTask SkipAsync(
            long advanceAmount,
            CancellationToken cancellationToken = default
        )
        {
            if (source.CanSeek && source is not SharpCompressStream)
            {
                source.Position += advanceAmount;
                return;
            }

            var array = ArrayPool<byte>.Shared.Rent(Constants.BufferSize);
            try
            {
                while (advanceAmount > 0)
                {
                    var toRead = (int)Math.Min(array.Length, advanceAmount);
                    var read = await source
                        .ReadAsync(array, 0, toRead, cancellationToken)
                        .ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    advanceAmount -= read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

#if NET8_0_OR_GREATER
        public bool ReadFully(byte[] buffer)
        {
            try
            {
                source.ReadExactly(buffer);
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }

        public bool ReadFully(Span<byte> buffer)
        {
            try
            {
                source.ReadExactly(buffer);
                return true;
            }
            catch (EndOfStreamException)
            {
                return false;
            }
        }
#else
        public bool ReadFully(byte[] buffer)
        {
            var total = 0;
            int read;
            while ((read = source.Read(buffer, total, buffer.Length - total)) > 0)
            {
                total += read;
                if (total >= buffer.Length)
                {
                    return true;
                }
            }

            return (total >= buffer.Length);
        }

        public bool ReadFully(Span<byte> buffer)
        {
            var total = 0;
            int read;
            while ((read = source.Read(buffer.Slice(total, buffer.Length - total))) > 0)
            {
                total += read;
                if (total >= buffer.Length)
                {
                    return true;
                }
            }

            return (total >= buffer.Length);
        }
#endif

        /// <summary>
        /// Read exactly the requested number of bytes from a stream. Throws EndOfStreamException if not enough data is available.
        /// </summary>
        public void ReadExact(byte[] buffer, int offset, int length)
        {
#if LEGACY_DOTNET
            if (source is null)
            {
                throw new ArgumentNullException();
            }
#else
            ThrowHelper.ThrowIfNull(source);
#endif

            ThrowHelper.ThrowIfNull(buffer);

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (length < 0 || length > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            while (length > 0)
            {
                var fetched = source.Read(buffer, offset, length);
                if (fetched <= 0)
                {
                    throw new IncompleteArchiveException("Unexpected end of stream.");
                }

                offset += fetched;
                length -= fetched;
            }
        }
    }

    public static string TrimNulls(this string source) => source.Replace('\0', ' ').Trim();

    /// <summary>
    /// Swap the endianness of a UINT32
    /// </summary>
    /// <param name="number">The UINT32 you want to swap his endianness</param>
    /// <returns>Return the new UINT32 in the other endianness format</returns>
    public static uint SwapUINT32(uint number) =>
        (number >> 24)
        | ((number << 8) & 0x00FF0000)
        | ((number >> 8) & 0x0000FF00)
        | (number << 24);

    /// <summary>
    /// Insert a little endian UINT32 into a byte array
    /// </summary>
    /// <param name="buffer">The buffer to insert into</param>
    /// <param name="number">The UINT32 to insert</param>
    /// <param name="offset">Offset of the buffer to insert into</param>
    public static void SetLittleUInt32(ref byte[] buffer, uint number, long offset)
    {
        buffer[offset] = (byte)(number);
        buffer[offset + 1] = (byte)(number >> 8);
        buffer[offset + 2] = (byte)(number >> 16);
        buffer[offset + 3] = (byte)(number >> 24);
    }

    /// <summary>
    /// Insert a big endian UINT32 into a byte array
    /// </summary>
    /// <param name="buffer">The buffer to insert into</param>
    /// <param name="number">The UINT32 to insert</param>
    /// <param name="offset">Offset of the buffer to insert into</param>
    public static void SetBigUInt32(ref byte[] buffer, uint number, long offset)
    {
        buffer[offset] = (byte)(number >> 24);
        buffer[offset + 1] = (byte)(number >> 16);
        buffer[offset + 2] = (byte)(number >> 8);
        buffer[offset + 3] = (byte)number;
    }

    public static string ReplaceInvalidFileNameChars(string fileName)
    {
        var sb = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
        {
            var newChar = invalidChars.Contains(c) ? '_' : c;
            sb.Append(newChar);
        }

        return sb.ToString();
    }
}
