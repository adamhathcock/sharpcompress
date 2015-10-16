namespace SharpCompress
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;

    internal static class Utility
    {
        public static void AddRange<T>(ICollection<T> destination, IEnumerable<T> source)
        {
            foreach (T local in source)
            {
                destination.Add(local);
            }
        }

        public static IEnumerable<T> AsEnumerable<T>(T item)
        {
            yield return item;
        }

        public static bool BinaryEquals(byte[] source, byte[] target)
        {
            if (source.Length != target.Length)
            {
                return false;
            }
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != target[i])
                {
                    return false;
                }
            }
            return true;
        }

        public static void CheckNotNull(object obj, string name)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        public static void CheckNotNullOrEmpty(string obj, string name)
        {
            CheckNotNull(obj, name);
            if (obj.Length == 0)
            {
                throw new ArgumentException("String is empty.");
            }
        }

        public static void CopyTo(byte[] array, byte[] destination, int index)
        {
            Array.Copy(array, 0, destination, index, array.Length);
        }

        public static uint DateTimeToDosTime(DateTime? dateTime)
        {
            if (!dateTime.HasValue)
            {
                return 0;
            }
            DateTime time = dateTime.Value.ToLocalTime();
            return (uint) ((((((time.Second / 2) | (time.Minute << 5)) | (time.Hour << 11)) | (time.Day << 0x10)) | (time.Month << 0x15)) | ((time.Year - 0x7bc) << 0x19));
        }

        public static DateTime DosDateToDateTime(int iTime)
        {
            return DosDateToDateTime((uint) iTime);
        }

        public static DateTime DosDateToDateTime(uint iTime)
        {
            return DosDateToDateTime((ushort) (iTime / 0x10000), (ushort) (iTime % 0x10000));
        }

        public static DateTime DosDateToDateTime(ushort iDate, ushort iTime)
        {
            int year = (iDate / 0x200) + 0x7bc;
            int month = (iDate % 0x200) / 0x20;
            int day = (iDate % 0x200) % 0x20;
            int hour = iTime / 0x800;
            int minute = (iTime % 0x800) / 0x20;
            int second = ((iTime % 0x800) % 0x20) * 2;
            if (((iDate == 0xffff) || (month == 0)) || (day == 0))
            {
                year = 0x7bc;
                month = 1;
                day = 1;
            }
            if (iTime == 0xffff)
            {
                hour = minute = second = 0;
            }
            try
            {
                return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
            }
            catch
            {
                return new DateTime();
            }
        }

        public static void Fill<T>(T[] array, T val) where T: struct
        {
            Fill<T>(array, 0, array.Length, val);
        }

        public static void Fill<T>(T[] array, int fromindex, int toindex, T val) where T: struct
        {
            if (array.Length == 0)
            {
                throw new NullReferenceException();
            }
            if (fromindex > toindex)
            {
                throw new ArgumentException();
            }
            if ((fromindex < 0) || (array.Length < toindex))
            {
                throw new IndexOutOfRangeException();
            }
            for (int i = (fromindex > 0) ? fromindex-- : fromindex; i < toindex; i++)
            {
                array[i] = val;
            }
        }

        public static void ForEach<T>(IEnumerable<T> items, Action<T> action)
        {
            foreach (T local in items)
            {
                action(local);
            }
        }

        public static short HostToNetworkOrder(short host)
        {
            return (short) (((host & 0xff) << 8) | ((host >> 8) & 0xff));
        }

        public static int HostToNetworkOrder(int host)
        {
            return (((HostToNetworkOrder((short) host) & -1) << 0x10) | (HostToNetworkOrder((short) (host >> 0x10)) & -1));
        }

        public static long HostToNetworkOrder(long host)
        {
            return (long) ((int) ((HostToNetworkOrder((int) host) & -4294967296L) | (HostToNetworkOrder((int) host) & -1L)));
        }

        public static void incShortLittleEndian(byte[] array, int pos, short incrementValue)
        {
            short num = (short) (BitConverter.ToInt16(array, pos) + incrementValue);
            WriteLittleEndian(array, pos, num);
        }

        public static void Initialize<T>(T[] array, Func<T> func)
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = func();
            }
        }

        public static short NetworkToHostOrder(short network)
        {
            return HostToNetworkOrder(network);
        }

        public static int NetworkToHostOrder(int network)
        {
            return HostToNetworkOrder(network);
        }

        public static long NetworkToHostOrder(long network)
        {
            return HostToNetworkOrder(network);
        }

        public static bool ReadFully(Stream stream, byte[] buffer)
        {
            int num2;
            int offset = 0;
            while ((num2 = stream.Read(buffer, offset, buffer.Length - offset)) > 0)
            {
                offset += num2;
                if (offset >= buffer.Length)
                {
                    return true;
                }
            }
            return (offset >= buffer.Length);
        }

        public static int readIntBigEndian(byte[] array, int pos)
        {
            int num = 0;
            num |= array[pos] & 0xff;
            num = num << 8;
            num |= array[pos + 1] & 0xff;
            num = num << 8;
            num |= array[pos + 2] & 0xff;
            num = num << 8;
            return (num | (array[pos + 3] & 0xff));
        }

        public static int readIntLittleEndian(byte[] array, int pos)
        {
            return BitConverter.ToInt32(array, pos);
        }

        public static short readShortLittleEndian(byte[] array, int pos)
        {
            return BitConverter.ToInt16(array, pos);
        }

        public static void SetSize(List<byte> list, int count)
        {
            if (count > list.Count)
            {
                for (int i = list.Count; i < count; i++)
                {
                    list.Add(0);
                }
            }
            else
            {
                byte[] array = new byte[count];
                list.CopyTo(array, 0);
                list.Clear();
                list.AddRange(array);
            }
        }

        public static void Skip(Stream source, long advanceAmount)
        {
            byte[] buffer = new byte[0x8000];
            int num = 0;
            int count = 0;
            while (true)
            {
                count = buffer.Length;
                if (count > advanceAmount)
                {
                    count = (int) advanceAmount;
                }
                num = source.Read(buffer, 0, count);
                if (num < 0)
                {
                    break;
                }
                advanceAmount -= num;
                if (advanceAmount == 0L)
                {
                    break;
                }
            }
        }

        public static void SkipAll(Stream source)
        {
            byte[] buffer = new byte[0x8000];
            while (source.Read(buffer, 0, buffer.Length) == buffer.Length)
            {
            }
        }

        public static ReadOnlyCollection<T> ToReadOnly<T>(IEnumerable<T> items)
        {
            return new ReadOnlyCollection<T>(Enumerable.ToList<T>(items));
        }

        public static long TransferTo(Stream source, Stream destination)
        {
            int num;
            byte[] buffer = new byte[0x14000];
            long num2 = 0L;
            while ((num = source.Read(buffer, 0, buffer.Length)) != 0)
            {
                num2 += num;
                destination.Write(buffer, 0, num);
            }
            return num2;
        }

        public static string TrimNulls(string source)
        {
            return source.Replace('\0', ' ').Trim();
        }

        public static byte[] UInt32ToBigEndianBytes(uint x)
        {
            return new byte[] { ((byte) ((x >> 0x18) & 0xff)), ((byte) ((x >> 0x10) & 0xff)), ((byte) ((x >> 8) & 0xff)), ((byte) (x & 0xff)) };
        }

        public static int URShift(int number, int bits)
        {
            if (number >= 0)
            {
                return (number >> bits);
            }
            return ((number >> bits) + (((int) 2) << ~bits));
        }

        public static int URShift(int number, long bits)
        {
            return URShift(number, (int) bits);
        }

        public static long URShift(long number, int bits)
        {
            if (number >= 0L)
            {
                return (number >> bits);
            }
            return ((number >> bits) + (((long) 2L) << ~bits));
        }

        public static long URShift(long number, long bits)
        {
            return URShift(number, (int) bits);
        }

        public static void writeIntBigEndian(byte[] array, int pos, int value)
        {
            array[pos] = (byte) (URShift(value, 0x18) & 0xff);
            array[pos + 1] = (byte) (URShift(value, 0x10) & 0xff);
            array[pos + 2] = (byte) (URShift(value, 8) & 0xff);
            array[pos + 3] = (byte) (value & 0xff);
        }

        public static void WriteLittleEndian(byte[] array, int pos, short value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, array, pos, bytes.Length);
        }

        public static void WriteLittleEndian(byte[] array, int pos, int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, array, pos, bytes.Length);
        }

        //[CompilerGenerated]
        //private sealed class _AsEnumerable_d__0<T> : IEnumerable<T>, IEnumerable, IEnumerator<T>, IEnumerator, IDisposable
        //{
        //    private int __1__state;
        //    private T __2__current;
        //    public T __3__item;
        //    private int __l__initialThreadId;
        //    public T item;

        //    [DebuggerHidden]
        //    public _AsEnumerable_d__0(int __1__state)
        //    {
        //        this.__1__state = __1__state;
        //        this.__l__initialThreadId = Thread.CurrentThread.ManagedThreadId;
        //    }

        //    public bool MoveNext()
        //    {
        //        switch (this.__1__state)
        //        {
        //            case 0:
        //                this.__1__state = -1;
        //                this.__2__current = this.item;
        //                this.__1__state = 1;
        //                return true;

        //            case 1:
        //                this.__1__state = -1;
        //                break;
        //        }
        //        return false;
        //    }

        //    [DebuggerHidden]
        //    IEnumerator<T> IEnumerable<T>.GetEnumerator()
        //    {
        //        Utility._AsEnumerable_d__0<T> d__;
        //        if ((Thread.CurrentThread.ManagedThreadId == this.__l__initialThreadId) && (this.__1__state == -2))
        //        {
        //            this.__1__state = 0;
        //            d__ = (Utility._AsEnumerable_d__0<T>) this;
        //        }
        //        else
        //        {
        //            d__ = new Utility._AsEnumerable_d__0<T>(0);
        //        }
        //        d__.item = this.__3__item;
        //        return d__;
        //    }

        //    [DebuggerHidden]
        //    IEnumerator IEnumerable.GetEnumerator()
        //    {
        //        return this.System.Collections.Generic.IEnumerable<T>.GetEnumerator();
        //    }

        //    [DebuggerHidden]
        //    void IEnumerator.Reset()
        //    {
        //        throw new NotSupportedException();
        //    }

        //    void IDisposable.Dispose()
        //    {
        //    }

        //    T IEnumerator<T>.Current
        //    {
        //        [DebuggerHidden]
        //        get
        //        {
        //            return this.__2__current;
        //        }
        //    }

        //    object IEnumerator.Current
        //    {
        //        [DebuggerHidden]
        //        get
        //        {
        //            return this.__2__current;
        //        }
        //    }
        //}
    }
}

