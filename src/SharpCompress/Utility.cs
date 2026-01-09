using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress;

internal static class Utility
{
    //80kb is a good industry standard temporary buffer size
    private const int TEMP_BUFFER_SIZE = 81920;
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

    public static void Skip(this Stream source, long advanceAmount)
    {
        if (source.CanSeek)
        {
            source.Position += advanceAmount;
            return;
        }

        using var readOnlySubStream = new IO.ReadOnlySubStream(source, advanceAmount);
        readOnlySubStream.CopyTo(Stream.Null);
    }

    public static void Skip(this Stream source) => source.CopyTo(Stream.Null);

    public static Task SkipAsync(this Stream source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return source.CopyToAsync(Stream.Null);
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

    public static long TransferTo(this Stream source, Stream destination, long maxLength)
    {
        var array = ArrayPool<byte>.Shared.Rent(TEMP_BUFFER_SIZE);
        try
        {
            var maxReadSize = array.Length;
            long total = 0;
            var remaining = maxLength;
            if (remaining < maxReadSize)
            {
                maxReadSize = (int)remaining;
            }
            while (ReadTransferBlock(source, array, maxReadSize, out var count))
            {
                destination.Write(array, 0, count);
                total += count;
                if (remaining - count < 0)
                {
                    break;
                }
                remaining -= count;
                if (remaining < maxReadSize)
                {
                    maxReadSize = (int)remaining;
                }
            }
            return total;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    public static async Task<long> TransferToAsync(
        this Stream source,
        Stream destination,
        long maxLength,
        CancellationToken cancellationToken = default
    )
    {
        var array = ArrayPool<byte>.Shared.Rent(TEMP_BUFFER_SIZE);
        try
        {
            var maxReadSize = array.Length;
            long total = 0;
            var remaining = maxLength;
            if (remaining < maxReadSize)
            {
                maxReadSize = (int)remaining;
            }
            while (
                await ReadTransferBlockAsync(source, array, maxReadSize, cancellationToken)
                    .ConfigureAwait(false)
                    is var (success, count)
                && success
            )
            {
                await destination
                    .WriteAsync(array, 0, count, cancellationToken)
                    .ConfigureAwait(false);
                total += count;
                if (remaining - count < 0)
                {
                    break;
                }
                remaining -= count;
                if (remaining < maxReadSize)
                {
                    maxReadSize = (int)remaining;
                }
            }
            return total;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    private static bool ReadTransferBlock(Stream source, byte[] array, int maxSize, out int count)
    {
        var size = maxSize;
        if (maxSize > array.Length)
        {
            size = array.Length;
        }
        count = source.Read(array, 0, size);
        return count != 0;
    }

    private static async Task<(bool success, int count)> ReadTransferBlockAsync(
        Stream source,
        byte[] array,
        int maxSize,
        CancellationToken cancellationToken
    )
    {
        var size = maxSize;
        if (maxSize > array.Length)
        {
            size = array.Length;
        }
        var count = await source.ReadAsync(array, 0, size, cancellationToken).ConfigureAwait(false);
        return (count != 0, count);
    }

    public static async Task SkipAsync(
        this Stream source,
        long advanceAmount,
        CancellationToken cancellationToken = default
    )
    {
        if (source.CanSeek)
        {
            source.Position += advanceAmount;
            return;
        }

        var array = ArrayPool<byte>.Shared.Rent(TEMP_BUFFER_SIZE);
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

#if NET60_OR_GREATER

    public static bool ReadFully(this Stream stream, byte[] buffer)
    {
        try
        {
            stream.ReadExactly(buffer);
            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    public static bool ReadFully(this Stream stream, Span<byte> buffer)
    {
        try
        {
            stream.ReadExactly(buffer);
            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }
#else
    public static bool ReadFully(this Stream stream, byte[] buffer)
    {
        var total = 0;
        int read;
        while ((read = stream.Read(buffer, total, buffer.Length - total)) > 0)
        {
            total += read;
            if (total >= buffer.Length)
            {
                return true;
            }
        }
        return (total >= buffer.Length);
    }

    public static bool ReadFully(this Stream stream, Span<byte> buffer)
    {
        var total = 0;
        int read;
        while ((read = stream.Read(buffer.Slice(total, buffer.Length - total))) > 0)
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

    public static async Task<bool> ReadFullyAsync(
        this Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken = default
    )
    {
        var total = 0;
        int read;
        while (
            (
                read = await stream
                    .ReadAsync(buffer, total, buffer.Length - total, cancellationToken)
                    .ConfigureAwait(false)
            ) > 0
        )
        {
            total += read;
            if (total >= buffer.Length)
            {
                return true;
            }
        }
        return (total >= buffer.Length);
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
