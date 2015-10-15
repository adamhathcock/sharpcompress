namespace SharpCompress.IO
{
    using System;
    using System.IO;

    public sealed class StreamUtils
    {
        private StreamUtils()
        {
        }

        public static void Copy(Stream source, Stream destination, byte[] buffer)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (destination == null)
            {
                throw new ArgumentNullException("destination");
            }
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (buffer.Length < 0x80)
            {
                throw new ArgumentException("Buffer is too small", "buffer");
            }
            bool flag = true;
            while (flag)
            {
                int count = source.Read(buffer, 0, buffer.Length);
                if (count > 0)
                {
                    destination.Write(buffer, 0, count);
                }
                else
                {
                    destination.Flush();
                    flag = false;
                }
            }
        }

        public static void Copy(Stream source, Stream destination, byte[] buffer, ProgressHandler progressHandler, TimeSpan updateInterval, object sender, string name)
        {
            Copy(source, destination, buffer, progressHandler, updateInterval, sender, name, -1L);
        }

        public static void Copy(Stream source, Stream destination, byte[] buffer, ProgressHandler progressHandler, TimeSpan updateInterval, object sender, string name, long fixedTarget)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (destination == null)
            {
                throw new ArgumentNullException("destination");
            }
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (buffer.Length < 0x80)
            {
                throw new ArgumentException("Buffer is too small", "buffer");
            }
            if (progressHandler == null)
            {
                throw new ArgumentNullException("progressHandler");
            }
            bool continueRunning = true;
            DateTime now = DateTime.Now;
            long processed = 0L;
            long target = 0L;
            if (fixedTarget >= 0L)
            {
                target = fixedTarget;
            }
            else if (source.CanSeek)
            {
                target = source.Length - source.Position;
            }
            ProgressEventArgs e = new ProgressEventArgs(name, processed, target);
            progressHandler(sender, e);
            bool flag2 = true;
            while (continueRunning)
            {
                int count = source.Read(buffer, 0, buffer.Length);
                if (count > 0)
                {
                    processed += count;
                    flag2 = false;
                    destination.Write(buffer, 0, count);
                }
                else
                {
                    destination.Flush();
                    continueRunning = false;
                }
                if ((DateTime.Now - now) > updateInterval)
                {
                    flag2 = true;
                    now = DateTime.Now;
                    e = new ProgressEventArgs(name, processed, target);
                    progressHandler(sender, e);
                    continueRunning = e.ContinueRunning;
                }
            }
            if (!flag2)
            {
                e = new ProgressEventArgs(name, processed, target);
                progressHandler(sender, e);
            }
        }

        public static void ReadFully(Stream stream, byte[] buffer)
        {
            ReadFully(stream, buffer, 0, buffer.Length);
        }

        public static void ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if ((offset < 0) || (offset > buffer.Length))
            {
                throw new ArgumentOutOfRangeException("offset");
            }
            if ((count < 0) || ((offset + count) > buffer.Length))
            {
                throw new ArgumentOutOfRangeException("count");
            }
            while (count > 0)
            {
                int num = stream.Read(buffer, offset, count);
                if (num <= 0)
                {
                    throw new EndOfStreamException();
                }
                offset += num;
                count -= num;
            }
        }
    }
}

