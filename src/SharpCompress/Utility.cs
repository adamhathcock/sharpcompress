using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Readers;

namespace SharpCompress
{
    internal static class Utility
    {
        public static async ValueTask ForEachAsync<T>(this IEnumerable<T> collection, Func<T, Task> action)
        {
            foreach (T item in collection)
            {
                await action(item);
            }
        }
        public static async ValueTask<T> ReadPrimitive<T>(this Stream stream, int bytes, Func<ReadOnlyMemory<byte>, T> func, CancellationToken cancellationToken)
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(bytes);
            await stream.ReadAsync(buffer.Memory.Slice(0, bytes), cancellationToken);
            return func(buffer.Memory.Slice(0, bytes));
        }
        
        public static ValueTask<byte> ReadByteAsync(this Stream stream, CancellationToken cancellationToken)
        {
            return stream.ReadPrimitive(1, x => x.Span[0], cancellationToken);
        }
        public static ValueTask<ushort> ReadUInt16(this Stream stream, CancellationToken cancellationToken)
        {
            return stream.ReadPrimitive(2, x => BinaryPrimitives.ReadUInt16LittleEndian(x.Span), cancellationToken);
        }
        public static ValueTask<uint> ReadUInt32(this Stream stream, CancellationToken cancellationToken)
        {
            return stream.ReadPrimitive(4, x => BinaryPrimitives.ReadUInt32LittleEndian(x.Span), cancellationToken);
        }
        public static ValueTask<int> ReadInt32(this Stream stream, CancellationToken cancellationToken)
        {
            return stream.ReadPrimitive(4, x => BinaryPrimitives.ReadInt32LittleEndian(x.Span), cancellationToken);
        }
        
        public static ValueTask<ulong> ReadUInt64(this Stream stream, CancellationToken cancellationToken)
        {
            return stream.ReadPrimitive(8, x => BinaryPrimitives.ReadUInt64LittleEndian(x.Span), cancellationToken);
        }

        public static async ValueTask<byte[]> ReadBytes(this Stream stream, int bytes, CancellationToken cancellationToken)
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(bytes);
            await stream.ReadAsync(buffer.Memory.Slice(0, bytes), cancellationToken);
            return buffer.Memory.Slice(0, bytes).ToArray();
        }
        
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
                for (int i = list.Count; i < count; i++)
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
            foreach (T item in items)
            {
                action(item);
            }
        }

        public static void Copy(Array sourceArray, long sourceIndex, Array destinationArray, long destinationIndex, long length)
        {
            if (sourceIndex > Int32.MaxValue || sourceIndex < Int32.MinValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (destinationIndex > Int32.MaxValue || destinationIndex < Int32.MinValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (length > Int32.MaxValue || length < Int32.MinValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            Array.Copy(sourceArray, (int)sourceIndex, destinationArray, (int)destinationIndex, (int)length);
        }

        public static IEnumerable<T> AsEnumerable<T>(this T item)
        {
            yield return item;
        }
        public static async IAsyncEnumerable<T> AsAsyncEnumerable<T>(this T item)
        {
            await Task.CompletedTask;
            yield return item;
        }

        public static T CheckNotNull<T>(this T? obj, string name)
            where T : class
        {
            if (obj is null)
            {
                throw new ArgumentNullException(name);
            }

            return obj;
        }

        public static void CheckNotNullOrEmpty(this string obj, string name)
        {
            obj.CheckNotNull(name);
            if (obj.Length == 0)
            {
                throw new ArgumentException("String is empty.", name);
            }
        }
        
        public static async ValueTask SkipAsync(this Stream source, long advanceAmount, CancellationToken cancellationToken = default)
        {
            if (source.CanSeek)
            {
                source.Position += advanceAmount;
                return;
            }

            using var buffer = MemoryPool<byte>.Shared.Rent(81920);
            do
            {
                var readCount = 81920;
                if (readCount > advanceAmount)
                {
                    readCount = (int)advanceAmount;
                }
                int read = await source.ReadAsync(buffer.Memory.Slice(0, readCount), cancellationToken);
                if (read <= 0)
                {
                    break;
                }
                advanceAmount -= read;
                if (advanceAmount == 0)
                {
                    break;
                }
            }
            while (true);
        }

        public static void Skip(this Stream source, long advanceAmount)
        {
            if (source.CanSeek)
            {
                source.Position += advanceAmount;
                return;
            }

            byte[] buffer = GetTransferByteArray();
            try
            {
                int read = 0;
                int readCount = 0;
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
                }
                while (true);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static void Skip(this Stream source)
        {
            byte[] buffer = GetTransferByteArray();
            try
            {
                do
                {
                }
                while (source.Read(buffer, 0, buffer.Length) == buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        
        public static async ValueTask SkipAsync(this Stream source, CancellationToken cancellationToken)
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(81920);
            do
            {
            }
            while (await source.ReadAsync(buffer.Memory.Slice(0, 81920), cancellationToken) > 0);
        }

        public static DateTime DosDateToDateTime(UInt16 iDate, UInt16 iTime)
        {
            int year = iDate / 512 + 1980;
            int month = iDate % 512 / 32;
            int day = iDate % 512 % 32;
            int hour = iTime / 2048;
            int minute = iTime % 2048 / 32;
            int second = iTime % 2048 % 32 * 2;

            if (iDate == UInt16.MaxValue || month == 0 || day == 0)
            {
                year = 1980;
                month = 1;
                day = 1;
            }

            if (iTime == UInt16.MaxValue)
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
                             (localDateTime.Second / 2) | (localDateTime.Minute << 5) | (localDateTime.Hour << 11) |
                             (localDateTime.Day << 16) | (localDateTime.Month << 21) |
                             ((localDateTime.Year - 1980) << 25));
        }

        public static DateTime DosDateToDateTime(UInt32 iTime)
        {
            return DosDateToDateTime((UInt16)(iTime / 65536),
                                     (UInt16)(iTime % 65536));
        }

        /// <summary>
        /// Convert Unix time value to a DateTime object.
        /// </summary>
        /// <param name="unixtime">The Unix time stamp you want to convert to DateTime.</param>
        /// <returns>Returns a DateTime object that represents value of the Unix time.</returns>
        public static DateTime UnixTimeToDateTime(long unixtime)
        {
            DateTime sTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return sTime.AddSeconds(unixtime);
        }
        
        public static async Task<long> TransferToAsync(this Stream source, Stream destination, CancellationToken cancellationToken)
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(81920);
            long total = 0;
            while (true)
            {
                int bytesRead = await source.ReadAsync(buffer.Memory.Slice(0, 81920), cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                total += bytesRead;
                await destination.WriteAsync(buffer.Memory.Slice(0, bytesRead), cancellationToken);
            }
            return total;
        }

        public static async ValueTask WriteByte(this Stream stream, byte b, CancellationToken cancellationToken = default)
        {
            await stream.WriteAsync(new ReadOnlyMemory<byte>(new[] {b}), cancellationToken);
        }

        public static long TransferTo(this Stream source, Stream destination)
        {
            byte[] array = GetTransferByteArray();
            try
            {
                long total = 0;
                var count = 0;
                while ((count = source.Read(array, 0, array.Length)) != 0)
                {
                    total += count;
                    destination.Write(array, 0, count);
                }
                return total;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        public static async ValueTask<long> TransferToAsync(this Stream source, Stream destination, Common.Entry entry, IReaderExtractionListener readerExtractionListener, CancellationToken cancellationToken)
        {
            using var buffer = MemoryPool<byte>.Shared.Rent(81920);
            var iterations = 0;
            long total = 0;
            var count = 0;
            var slice = buffer.Memory.Slice(0, 81920);
            while ((count = await source.ReadAsync(slice, cancellationToken)) != 0)
            {
                total += count;
                await destination.WriteAsync(slice.Slice(0, count), cancellationToken);
                iterations++;
                readerExtractionListener.FireEntryExtractionProgress(entry, total, iterations);
            }
            return total;
        }

        private static byte[] GetTransferByteArray()
        {
            return ArrayPool<byte>.Shared.Rent(81920);
        }

        public static bool ReadFully(this Stream stream, Span<byte> buffer)
        {
            int total = 0;
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

        public static string TrimNulls(this string source)
        {
            return source.Replace('\0', ' ').Trim();
        }
    }
}
