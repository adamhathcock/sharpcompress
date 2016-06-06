using System.IO;

namespace SharpCompress.Test
{
   public class TestStream : Stream
   {
      private Stream stream;
      private bool read;
      private bool write;
      private bool seek;

      public TestStream(Stream stream)
         : this(stream, true, true, true)
      {
      }

      public bool IsDisposed { get; private set; }

      public TestStream(Stream stream, bool read, bool write, bool seek)
      {
         this.stream = stream;
         this.read = read;
         this.write = write;
         this.seek = seek;
      }

      protected override void Dispose(bool disposing)
      {
         base.Dispose(disposing);
         stream.Dispose();
         IsDisposed = true;
      }

      public override bool CanRead
      {
         get { return read; }
      }

      public override bool CanSeek
      {
         get { return seek; }
      }

      public override bool CanWrite
      {
         get { return write; }
      }

      public override void Flush()
      {
         stream.Flush();
      }

      public override long Length
      {
         get { return stream.Length; }
      }

      public override long Position
      {
         get { return stream.Position; }
         set { stream.Position = value; }
      }

      public override int Read(byte[] buffer, int offset, int count)
      {
         return stream.Read(buffer, offset, count);
      }

      public override long Seek(long offset, SeekOrigin origin)
      {
         return stream.Seek(offset, origin);
      }

      public override void SetLength(long value)
      {
         stream.SetLength(value);
      }

      public override void Write(byte[] buffer, int offset, int count)
      {
         stream.Write(buffer, offset, count);
      }
   }
}
