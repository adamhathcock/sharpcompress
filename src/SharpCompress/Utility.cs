global using SharpCompress.Helpers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using SharpCompress.Readers;

namespace SharpCompress.Helpers;

internal static class Utility
{
    public static ReadOnlyCollection<T> ToReadOnly<T>(this IList<T> items) => new(items);

    /// <summary>
    /// Performs an unsigned bitwise right shift with the specified number
    /// </summary>
    /// <param name="number">Number to operate on</param>
    /// <param name="bits">Amount of bits to shift</param>
    /// <returns>The resulting number from the shift operation</returns>
    public static int URShift(int number, int bits)
    {
        if (number >= 0)
        {
            return number >> bits;
        }
        return (number >> bits) + (2 << ~bits);
    }

    /// <summary>
    /// Performs an unsigned bitwise right shift with the specified number
    /// </summary>
    /// <param name="number">Number to operate on</param>
    /// <param name="bits">Amount of bits to shift</param>
    /// <returns>The resulting number from the shift operation</returns>
    public static long URShift(long number, int bits)
    {
        if (number >= 0)
        {
            return number >> bits;
        }
        return (number >> bits) + (2L << ~bits);
    }

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

    public static void Copy(
        Array sourceArray,
        long sourceIndex,
        Array destinationArray,
        long destinationIndex,
        long length
    )
    {
        if (sourceIndex > int.MaxValue || sourceIndex < int.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        }

        if (destinationIndex > int.MaxValue || destinationIndex < int.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationIndex));
        }

        if (length > int.MaxValue || length < int.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Array.Copy(
            sourceArray,
            (int)sourceIndex,
            destinationArray,
            (int)destinationIndex,
            (int)length
        );
    }

    public static IEnumerable<T> AsEnumerable<T>(this T item)
    {
        yield return item;
    }

    public static void CheckNotNull(this object obj, string name)
    {
        if (obj is null)
        {
            throw new ArgumentNullException(name);
        }
    }

    public static void CheckNotNullOrEmpty(this string obj, string name)
    {
        obj.CheckNotNull(name);
        if (obj.Length == 0)
        {
            throw new ArgumentException("String is empty.", name);
        }
    }

    public static void Skip(this Stream source, long advanceAmount)
    {
        if (source.CanSeek)
        {
            source.Position += advanceAmount;
            return;
        }

        var buffer = GetTransferByteArray();
        try
        {
            var read = 0;
            var readCount = 0;
            do
            {
                readCount = buffer.Length;
                if (readCount > advanceAmount)
                {
                    readCount = (int)advanceAmount;
                }
                read = source.Read(buffer, 0, readCount);
                if (read <= 0)
                {
                    break;
                }
                advanceAmount -= read;
                if (advanceAmount == 0)
                {
                    break;
                }
            } while (true);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static void Skip(this Stream source)
    {
        var buffer = GetTransferByteArray();
        try
        {
            do { } while (source.Read(buffer, 0, buffer.Length) == buffer.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static bool Find(this Stream source, byte[] array)
    {
        var buffer = GetTransferByteArray();
        try
        {
            var count = 0;
            var len = source.Read(buffer, 0, buffer.Length);

            do
            {
                for (var i = 0; i < len; i++)
                {
                    if (array[count] == buffer[i])
                    {
                        count++;
                        if (count == array.Length)
                        {
                            source.Position = source.Position - len + i - array.Length + 1;
                            return true;
                        }
                    }
                }
            } while ((len = source.Read(buffer, 0, buffer.Length)) > 0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return false;
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

    public static long TransferTo(this Stream source, Stream destination)
    {
        var array = GetTransferByteArray();
        try
        {
            long total = 0;
            while (ReadTransferBlock(source, array, out var count))
            {
                destination.Write(array, 0, count);
                total += count;
            }
            return total;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    public static long TransferTo(this Stream source, Stream destination, long maxLength)
    {
        var array = GetTransferByteArray();
        var maxReadSize = array.Length;
        try
        {
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

    public static long TransferTo(
        this Stream source,
        Stream destination,
        Common.Entry entry,
        IReaderExtractionListener readerExtractionListener
    )
    {
        var array = GetTransferByteArray();
        try
        {
            var iterations = 0;
            long total = 0;
            while (ReadTransferBlock(source, array, out var count))
            {
                total += count;
                destination.Write(array, 0, count);
                iterations++;
                readerExtractionListener.FireEntryExtractionProgress(entry, total, iterations);
            }
            return total;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    private static bool ReadTransferBlock(Stream source, byte[] array, out int count) =>
        (count = source.Read(array, 0, array.Length)) != 0;

    private static bool ReadTransferBlock(Stream source, byte[] array, int size, out int count)
    {
        if (size > array.Length)
        {
            size = array.Length;
        }
        count = source.Read(array, 0, size);
        return count != 0;
    }

    private static byte[] GetTransferByteArray() => ArrayPool<byte>.Shared.Rent(81920);

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
        var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
        var sb = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
        {
            var newChar = invalidChars.Contains(c) ? '_' : c;
            sb.Append(newChar);
        }

        return sb.ToString();
    }
}
