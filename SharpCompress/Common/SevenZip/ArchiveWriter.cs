using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// TODO: Replace BinaryWriter by a custom one which is always little-endian.

// Design:
// - constructor writes header for empty 7z archive
// - call methods to add content to the archive
//   this will also build up header information in memory
// - call WriteFinalHeader to output the secondary header and update the primary header
//
// In any case the archive will be valid, but unless you call WriteFinalHeader
// it will just be an empty archive possibly followed by unreachable content.
//

namespace ManagedLzma.LZMA.Master.SevenZip
{
    // TODO: Can we get rid of this interface and just pass the arguments directly to BeginWriteStream?
    public interface IArchiveWriterEntry
    {
        string Name { get; }
        FileAttributes? Attributes { get; }
        DateTime? CreationTime { get; }
        DateTime? LastWriteTime { get; }
        DateTime? LastAccessTime { get; }
    }

    internal static class FileNameHelper
    {
        public static string CalculateName(DirectoryInfo root, DirectoryInfo folder)
        {
            if(root.Root.FullName != folder.Root.FullName)
                throw new InvalidOperationException("Unrelated directories.");

            Stack<DirectoryInfo> rootList = new Stack<DirectoryInfo>();
            while(root.FullName != root.Root.FullName)
            {
                rootList.Push(root);
                root = root.Parent;
            }

            Stack<DirectoryInfo> itemList = new Stack<DirectoryInfo>();
            while(folder.FullName != folder.Root.FullName)
            {
                itemList.Push(folder);
                folder = folder.Parent;
            }

            while(rootList.Count != 0 && itemList.Count != 0 && rootList.Peek().Name == itemList.Peek().Name)
            {
                rootList.Pop();
                itemList.Pop();
            }

            if(rootList.Count != 0)
                throw new InvalidOperationException("Item is not contained in root.");

            if(itemList.Count == 0)
                return null;

            return String.Join("/", itemList.Select(item => item.Name));
        }

        public static string CalculateName(DirectoryInfo root, FileInfo file)
        {
            string path = CalculateName(root, file.Directory);
            return String.IsNullOrEmpty(path) ? file.Name : path + "/" + file.Name;
        }
    }

    internal sealed class FileBasedArchiveWriterEntry: IArchiveWriterEntry
    {
        private FileInfo mFile;
        private string mName;

        public FileBasedArchiveWriterEntry(DirectoryInfo root, FileInfo file)
        {
            mFile = file;
            mName = FileNameHelper.CalculateName(root, file);
        }

        public string Name
        {
            get { return mName; }
        }

        public FileAttributes? Attributes
        {
            get { return mFile.Attributes; }
        }

        public DateTime? CreationTime
        {
            get { return mFile.CreationTimeUtc; }
        }

        public DateTime? LastWriteTime
        {
            get { return mFile.LastWriteTimeUtc; }
        }

        public DateTime? LastAccessTime
        {
            get { return mFile.LastAccessTimeUtc; }
        }
    }

    /// <summary>
    /// MemoryStream which uses chunks of memory instead of one big byte array.
    /// </summary>
    internal sealed class FragmentedMemoryStream: Stream
    {
        #region Constants

        // Chunk size should be small enough for buffers to not be placed on
        // the large object heap, so they stay relocable and don't fragment.
        // Objects are placed on the LOH if they are larger than 85000 bytes,
        // so 64K buffers should be fine for us.

        private const int kChunkShift = 16;
        private const int kChunkSize = 1 << kChunkShift;
        private const int kChunkMask = kChunkSize - 1;

        #endregion

        #region Variables

        private List<byte[]> mChunks = new List<byte[]>();
        private int mOffset;
        private int mEnding;

        #endregion

        #region Public Methods

        public FragmentedMemoryStream()
        {
        }

        public FragmentedMemoryStream(int capacity)
        {
            EnsureCapacity(capacity);
        }

        public int Capacity
        {
            get { return mChunks.Count << kChunkShift; }
        }

        public void ReduceCapacity()
        {
            int requiredChunks = (mEnding + kChunkMask) >> kChunkShift;

            if(mChunks.Count > requiredChunks)
                mChunks.RemoveRange(requiredChunks, mChunks.Count - requiredChunks);
        }

        public void ReduceCapacity(int capacity)
        {
            if(capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            // Limit stream size to prevent overflows in calculations.
            if(capacity > Int32.MaxValue - kChunkMask)
                throw new NotSupportedException("Large streams are not supported.");

            int requiredChunks = (capacity + kChunkMask) >> kChunkShift;

            if(mChunks.Count > requiredChunks)
                mChunks.RemoveRange(requiredChunks, mChunks.Count - requiredChunks);
        }

        public void EnsureCapacity(int capacity)
        {
            if(capacity < 0)
                throw new ArgumentOutOfRangeException("capacity");

            // Limit stream size to prevent overflows in calculations.
            if(capacity > Int32.MaxValue - kChunkMask)
                throw new NotSupportedException("Large streams are not supported.");

            int requiredChunks = (capacity + kChunkMask) >> kChunkShift;

            while(mChunks.Count < requiredChunks)
                mChunks.Add(new byte[kChunkSize]);
        }

        #endregion

        public void FullCopyTo(Stream destination)
        {
            int fullCount = mEnding >> kChunkShift;
            for(int i = 0; i < fullCount; i++)
                destination.Write(mChunks[i], 0, kChunkSize);

            int remaining = mEnding & kChunkMask;
            if(remaining != 0)
                destination.Write(mChunks[fullCount], 0, remaining);
        }

        #region Stream Implementation

        protected override void Dispose(bool disposing)
        {
            if(disposing)
                mChunks = null;

            base.Dispose(disposing);
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override long Position
        {
            get { return mOffset; }
            set
            {
                if(value < 0 || value > mEnding)
                    throw new ArgumentOutOfRangeException("value");

                mOffset = (int)value;
            }
        }

        public override long Length
        {
            get { return mEnding; }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch(origin)
            {
            case SeekOrigin.Begin:
                return Position = offset;
            case SeekOrigin.Current:
                return Position += offset;
            case SeekOrigin.End:
                return Position = Length + offset;
            default:
                throw new ArgumentOutOfRangeException("origin");
            }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if(buffer == null)
                throw new ArgumentNullException("buffer");

            if(offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            if(count < 0 || count > buffer.Length - offset)
                throw new ArgumentOutOfRangeException("count");

            int remaining = mEnding - mOffset;
            if(count > remaining)
                count = remaining;

            if(count == 0)
                return 0;

            byte[] chunk = mChunks[mOffset >> kChunkShift];
            int chunkOffset = mOffset & kChunkMask;
            int readLength = Math.Min(kChunkSize - chunkOffset, count);
            mOffset += readLength;
            Buffer.BlockCopy(chunk, chunkOffset, buffer, offset, readLength);
            return readLength;
        }

        public override int ReadByte()
        {
            if(mOffset == mEnding)
                return -1;

            byte value = mChunks[mOffset >> kChunkShift][mOffset & kChunkMask];
            mOffset++;
            return value;
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush() { }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if(buffer == null)
                throw new ArgumentNullException("buffer");

            if(offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException("offset");

            if(count < 0 || count > buffer.Length - offset)
                throw new ArgumentOutOfRangeException("count");

            int remaining = mEnding - mOffset;
            if(count > remaining)
            {
                SetLength((long)mOffset + (long)count);
                remaining = mEnding - mOffset;
            }

            while(count > 0)
            {
                byte[] chunk = mChunks[mOffset >> kChunkShift];
                int chunkOffset = mOffset & kChunkMask;
                int writeLength = Math.Min(kChunkSize - chunkOffset, count);
                mOffset += writeLength;
                Buffer.BlockCopy(buffer, offset, chunk, chunkOffset, writeLength);
                offset += writeLength;
                count -= writeLength;
            }
        }

        public override void WriteByte(byte value)
        {
            if(mOffset == mEnding)
                SetLength(mEnding + 1);

            mChunks[mOffset >> kChunkShift][mOffset & kChunkMask] = value;
            mOffset++;
        }

        public override void SetLength(long value)
        {
            if(value < 0)
                throw new ArgumentOutOfRangeException("value");

            // Limit stream size to prevent overflows in calculations.
            if(value > Int32.MaxValue - kChunkMask)
                throw new NotSupportedException("Large streams are not supported.");

            mEnding = (int)value;

            if(mOffset > mEnding)
                mOffset = mEnding;

            int requiredChunks = (mEnding + kChunkMask) >> kChunkShift;

            // TODO: figure out a reasonable strategy about how many chunks to retain

            //if(mChunks.Count >= requiredChunks)
            //{
            //    // keep one chunk more than required, in case we start writing again
            //    if(mChunks.Count > ++requiredChunks)
            //        mChunks.RemoveRange(requiredChunks, mChunks.Count - requiredChunks);
            //    return;
            //}

            while(mChunks.Count < requiredChunks)
                mChunks.Add(new byte[kChunkSize]);
        }

        #endregion
    }

    public class ArchiveWriter
    {
        #region Configuration Elements

        internal abstract class StreamRef
        {
            public abstract long GetSize(FileSet fileset);
            public abstract uint? GetHash(FileSet fileset);
        }

        internal class InputStreamRef: StreamRef
        {
            public int PackedStreamIndex;

            public override long GetSize(FileSet fileset)
            {
                return fileset.InputStreams[PackedStreamIndex].Size;
            }

            public override uint? GetHash(FileSet fileset)
            {
                return fileset.InputStreams[PackedStreamIndex].Hash;
            }
        }

        internal class CoderStreamRef: StreamRef
        {
            public int CoderIndex;
            public int StreamIndex;

            public override long GetSize(FileSet fileset)
            {
                return fileset.Coders[CoderIndex].OutputStreams[StreamIndex].Size;
            }

            public override uint? GetHash(FileSet fileset)
            {
                return fileset.Coders[CoderIndex].OutputStreams[StreamIndex].Hash;
            }
        }

        internal class Coder
        {
            public master._7zip.Legacy.CMethodId MethodId;
            public byte[] Settings;
            public StreamRef[] InputStreams;
            public CoderStream[] OutputStreams;
        }

        internal class CoderStream
        {
            public long Size;
            public uint? Hash;
        }

        internal class InputStream
        {
            public long Size;
            public uint? Hash;
        }

        internal class FileSet
        {
            public InputStream[] InputStreams;
            public Coder[] Coders;
            public StreamRef DataStream;
            public FileEntry[] Files;
        }

        internal class FileEntry
        {
            public string Name;
            public uint? Flags;
            public DateTime? CTime;
            public DateTime? MTime;
            public DateTime? ATime;
            public long Size;
            public uint Hash;
        }

        #endregion

        #region Encoders

        internal sealed class EncoderStream: Stream
        {
            private long mStreamSize;
            private Encoder mEncoder;
            private uint mCRC = CRC.kInitCRC;

            internal EncoderStream(Encoder encoder)
            {
                mEncoder = encoder;
            }

            protected override void Dispose(bool disposing)
            {
                if(disposing && mEncoder != null)
                {
                    mEncoder = null;
                    mCRC = ~mCRC;
                }

                base.Dispose(disposing);
            }

            public sealed override bool CanRead
            {
                get { return false; }
            }

            public sealed override bool CanSeek
            {
                get { return false; }
            }

            public sealed override bool CanWrite
            {
                get { return true; }
            }

            public sealed override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public sealed override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public sealed override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public sealed override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Flush() { }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if(buffer == null)
                    throw new ArgumentNullException("buffer");

                if(offset < 0 || offset > buffer.Length)
                    throw new ArgumentOutOfRangeException("offset");

                if(count < 0 || count > buffer.Length - offset)
                    throw new ArgumentOutOfRangeException("count");

                mStreamSize += count;

                mEncoder.WriteInput(buffer, offset, count);
            }

            internal void Close(FileEntry file)
            {
                Close();

                file.Hash = mCRC;
                file.Size = mStreamSize;
            }
        }

        internal sealed class BufferedFileSet
        {
            private FileSet mMetadata;
            private FragmentedMemoryStream mBuffer;

            public BufferedFileSet(FileSet metadata, FragmentedMemoryStream buffer)
            {
                mMetadata = metadata;
                mBuffer = buffer;
            }

            public FileSet Metadata
            {
                get { return mMetadata; }
            }

            public FragmentedMemoryStream Buffer
            {
                get { return mBuffer; }
            }
        }

        public abstract class Encoder: IDisposable
        {
            private static DateTime? EnsureUTC(DateTime? value)
            {
                if(value.HasValue)
                    return value.Value.ToUniversalTime();
                else
                    return null;
            }

            private long mInputSize;
            private long mOutputSize;
            private ArchiveWriter mWriter;
            private FragmentedMemoryStream mBuffer;
            private EncoderStream mCurrentStream;
            private List<FileEntry> mFiles;
            private FileEntry mCurrentFile;
            private List<BufferedFileSet> mFlushed;

            public abstract long LowerBound { get; }
            public abstract long UpperBound { get; }

            internal Encoder() { }

            public virtual void Dispose()
            {
                Disconnect();
            }

            internal void Connect(ArchiveWriter writer)
            {
                Debug.Assert(writer != null && mWriter == null);

                mWriter = writer;

                if(mFlushed != null && mFlushed.Count != 0)
                {
                    foreach(var item in mFlushed)
                        mWriter.Encoder_WriteFileSet(item);

                    mFlushed.Clear();
                }

                if(mBuffer != null)
                {
                    mBuffer.FullCopyTo(writer.mFileStream);
                    mBuffer.SetLength(0);
                }
            }

            public void Disconnect()
            {
                if(IsConnected)
                {
                    Flush();

                    Debug.Assert(mWriter.mEncoder == this);
                    mWriter.mEncoder = null;
                    mWriter = null;
                }
            }

            private void FinishCurrentFile()
            {
                if(mCurrentFile != null)
                {
                    mCurrentStream.Close(mCurrentFile);
                    mCurrentStream = null;

                    if(mFiles == null)
                        mFiles = new List<FileEntry>();

                    mFiles.Add(mCurrentFile);
                    mCurrentFile = null;
                }
            }

            public ArchiveWriter ArchiveWriter
            {
                get { return mWriter; }
            }

            public bool IsConnected
            {
                get { return mWriter != null; }
            }

            public Stream BeginWriteFile(IArchiveWriterEntry file)
            {
                FinishCurrentFile();

                if(file == null)
                    throw new ArgumentNullException("file");

                mCurrentFile = new FileEntry {
                    Name = file.Name,
                    CTime = EnsureUTC(file.CreationTime),
                    MTime = EnsureUTC(file.LastWriteTime),
                    ATime = EnsureUTC(file.LastAccessTime),
                };

                var attributes = file.Attributes;
                if(attributes.HasValue)
                    mCurrentFile.Flags = (uint)attributes.Value;

                if(mWriter == null && mBuffer == null)
                    mBuffer = new FragmentedMemoryStream();

                return mCurrentStream = new EncoderStream(this);
            }

            /// <summary>
            /// Closes the currently opened file stream and flushes buffered metadata.
            /// </summary>
            public void Flush()
            {
                FinishCurrentFile();

                if(mFiles != null && mFiles.Count != 0)
                {
                    PrepareFinishFileSet();
                    var fileset = FinishFileSet(mFiles.ToArray(), mInputSize, mOutputSize);
                    mFiles.Clear();
                    mInputSize = 0;
                    mOutputSize = 0;

                    if(IsConnected)
                    {
                        mWriter.Encoder_FinishFileSet(fileset);
                    }
                    else
                    {
                        if(mFlushed == null)
                            mFlushed = new List<BufferedFileSet>();

                        Debug.Assert(mBuffer != null);
                        mFlushed.Add(new BufferedFileSet(fileset, mBuffer));
                        mBuffer = null;
                    }
                }
            }

            /// <summary>
            /// Write flushed data to an ArchiveWriter without connecting to it.
            /// If the ArchiveWriter has a connected encoder it is flushed first.
            /// This does not flush this encoder, if that is required do it manually.
            /// </summary>
            public void WriteFlushedData(ArchiveWriter writer)
            {
                if(writer == null)
                    throw new ArgumentNullException("writer");

                // If the writer has a connected encoder we need to flush that one.
                if(writer.mEncoder != null)
                    writer.mEncoder.Flush();

                if(mFlushed != null && mFlushed.Count != 0)
                {
                    foreach(var item in mFlushed)
                        writer.Encoder_WriteFileSet(item);

                    mFlushed.Clear();
                }
            }

            internal virtual void PrepareFinishFileSet() { }
            internal abstract FileSet FinishFileSet(FileEntry[] entries, long inputSize, long outputSize);
            internal abstract void OnWriteInput(byte[] buffer, int offset, int length);

            internal void WriteInput(byte[] buffer, int offset, int length)
            {
                OnWriteInput(buffer, offset, length);

                // If we didn't throw we assume the input was processed.
                mInputSize += length;
            }

            #region Protected Methods

            protected long CurrentInputSize
            {
                get { return mInputSize; }
            }

            protected long CurrentOutputSize
            {
                get { return mOutputSize; }
            }

            protected void WriteOutput(byte[] buffer, int offset, int length)
            {
                if(mWriter != null)
                    mWriter.mFileStream.Write(buffer, offset, length);
                else
                    mBuffer.Write(buffer, offset, length);

                // If we didn't throw we assume the output was processed.
                mOutputSize += length;
            }

            #endregion
        }

        public sealed class PlainEncoder: Encoder
        {
            public PlainEncoder() { }

            public override long LowerBound
            {
                get { return CurrentOutputSize; }
            }

            public override long UpperBound
            {
                get { return CurrentOutputSize; }
            }

            internal override void OnWriteInput(byte[] buffer, int offset, int length)
            {
                WriteOutput(buffer, offset, length);
            }

            internal override FileSet FinishFileSet(FileEntry[] entries, long inputSize, long outputSize)
            {
                Debug.Assert(inputSize == outputSize);

                return new FileSet {
                    Files = entries,
                    DataStream = new InputStreamRef { PackedStreamIndex = 0 },
                    InputStreams = new[] { new InputStream { Size = outputSize } },
                    Coders = new[] { new Coder {
                        MethodId = master._7zip.Legacy.CMethodId.kCopy,
                        Settings = null,
                        InputStreams = new[] { new InputStreamRef { PackedStreamIndex = 0 } },
                        OutputStreams = new[] { new CoderStream { Size = inputSize } },
                    } },
                };
            }
        }

        public abstract class ThreadedEncoder: Encoder
        {
            private enum State
            {
                Ready = 0,
                Flush = 1,
                Dispose = 2,
            }

            private const int kBufferLength = 1 << 20;

            private object mSyncObject;
            private Thread mEncoderThread;
            private State mState;
            private byte[] mInputBuffer;
            private int mInputOffset;
            private int mInputEnding;
            private int mBufferOffset; // not locked (not accessed by thread)
            private int mBufferEnding; // not locked (not accessed by thread)

            // HACK: To allow the user to react when the buffering crosses a certain threshold. Need a better API for this.
            private int mOutputThreshold;
            private bool mTriggerOutputThreshold;
            public event EventHandler OnOutputThresholdReached;
            public void SetOutputThreshold(int threshold)
            {
                lock(mSyncObject)
                {
                    mTriggerOutputThreshold = false;
                    mOutputThreshold = threshold;
                }
            }

            internal ThreadedEncoder()
            {
                mSyncObject = new object();
                mInputBuffer = new byte[kBufferLength];
                mBufferEnding = kBufferLength - 1;
            }

            protected void StartThread(string threadName)
            {
                mEncoderThread = new Thread(EncoderThread);
                mEncoderThread.Name = threadName;
                mEncoderThread.Start();
            }

            public override void Dispose()
            {
                try
                {
                    base.Dispose();
                }
                finally
                {
                    lock(mSyncObject)
                    {
                        if(mState == State.Dispose)
                            goto skip;

                        // If we are in flushed state the owner thread should be blocking inside FinishFileSet.
                        // So getting here in flushed state means some foreign thread called us, which is invalid.
                        Utils.Assert(mState == State.Ready);
                        mState = State.Dispose;
                        Monitor.Pulse(mSyncObject);
                    }

                    mEncoderThread.Join();

                skip: { }
                }
            }

            internal sealed override void OnWriteInput(byte[] buffer, int offset, int count)
            {
                if(mInputBuffer == null)
                    throw new ObjectDisposedException(null);

                // HACK: this is really ugly and probably hurts performance, but whatever, don't have a better solution currently
                //       it must be done at this place because at the place where we calculate the threshold we are on the wrong thread
                bool trigger = false;
                lock(mSyncObject)
                {
                    if(mTriggerOutputThreshold)
                    {
                        mTriggerOutputThreshold = false;
                        trigger = true;
                        System.Diagnostics.Debug.WriteLine("output threshold triggered");
                    }
                }
                if(trigger)
                {
                    var handler = OnOutputThresholdReached;
                    if(handler != null)
                        handler(this, EventArgs.Empty);
                }
                // HACK END

                while(count > 0)
                {
                    int copy;
                    if(mBufferOffset <= mBufferEnding)
                        copy = Math.Min(count, mBufferEnding - mBufferOffset);
                    else
                        copy = Math.Min(count, kBufferLength - mBufferOffset);

                    if(copy > 0)
                    {
                        Buffer.BlockCopy(buffer, offset, mInputBuffer, mBufferOffset, copy);
                        count -= copy;
                        offset += copy;
                        mBufferOffset = (mBufferOffset + copy) % kBufferLength;
                    }

                    lock(mSyncObject)
                    {
                        if(copy > 0)
                        {
                            mInputEnding = mBufferOffset;
                            Monitor.Pulse(mSyncObject);
                        }

                        for(; ; )
                        {
                            if(mState != State.Ready)
                                throw new ObjectDisposedException(null);

                            int offsetMinusOne = (mInputOffset + kBufferLength - 1) % kBufferLength;
                            if(mInputEnding != offsetMinusOne)
                            {
                                mBufferEnding = offsetMinusOne;
                                break;
                            }

                            Monitor.Wait(mSyncObject);
                        }
                    }
                }
            }

            internal override void PrepareFinishFileSet()
            {
                lock(mSyncObject)
                {
                    Utils.Assert(mState == State.Ready);

                    while(mInputOffset != mInputEnding)
                        Monitor.Wait(mSyncObject);

                    mState = State.Flush;
                    Monitor.Pulse(mSyncObject);

                    do { Monitor.Wait(mSyncObject); }
                    while(mState == State.Flush);
                }
            }

            private void EncoderThread()
            {
                for(; ; )
                {
                    EncoderThreadLoop();

                    lock(mSyncObject)
                    {
                        if(mState == State.Dispose)
                            return;

                        Utils.Assert(mState == State.Flush);
                        mState = State.Ready;
                        Monitor.Pulse(mSyncObject);
                    }
                }
            }

            protected abstract void EncoderThreadLoop();

            protected int ReadInputAsync(byte[] buffer, int offset, int length)
            {
                lock(mSyncObject)
                {
                    for(; ; )
                    {
                        if(mState != State.Ready)
                            return 0;

                        if(mInputOffset != mInputEnding)
                        {
                            int size;
                            if(mInputOffset <= mInputEnding)
                                size = Math.Min(length, mInputEnding - mInputOffset);
                            else
                                size = Math.Min(length, kBufferLength - mInputOffset);

                            Utils.Assert(size != 0);
                            Buffer.BlockCopy(mInputBuffer, mInputOffset, buffer, offset, size);
                            mInputOffset = (mInputOffset + size) % kBufferLength;
                            Monitor.Pulse(mSyncObject);
                            return size;
                        }

                        Monitor.Wait(mSyncObject);
                    }
                }
            }

            protected void WriteOutputAsync(byte[] buffer, int offset, int length)
            {
                // TODO: is it sane to write without lock here?
                //       need to check both for buffered and connected output cases
                //       -> it probably is not sane because the base class increments that CurrentOutputSize counter
                //          which is also read from the main thread to estimate bounds -> need to fix that counter issue
                WriteOutput(buffer, offset, length);

                // HACK: this doesn't really belong here, but I don't have any idea how to provide equivalent functionality
                //       maybe let the caller provide the buffer stream so the caller can hook into it through subclassing
                //       (note that we are on the wrong thread and can't call the event handler directly)
                lock(mSyncObject)
                {
                    if(mOutputThreshold > 0)
                    {
                        mOutputThreshold -= length;
                        if(mOutputThreshold <= 0)
                        {
                            mTriggerOutputThreshold = true;
                            System.Diagnostics.Debug.WriteLine("output threshold reached");
                        }
                    }
                }
            }
        }

        public sealed class LzmaEncoder: ThreadedEncoder
        {
            private long mLowerBound;
            private object mSyncObject;
            private LZMA.CSeqOutStream mOutputHelper;
            private LZMA.CSeqInStream mInputHelper;
            private byte[] mSettings;

            public LzmaEncoder()
            {
                mSyncObject = new object();
                mOutputHelper = new LZMA.CSeqOutStream(WriteOutputHelper);
                mInputHelper = new LZMA.CSeqInStream(ReadInputHelper);
                StartThread("LZMA Stream Buffer Thread");
            }

            public override long LowerBound
            {
                get
                {
                    lock(mSyncObject)
                        return mLowerBound;
                }
            }

            public override long UpperBound
            {
                get { return CurrentInputSize; }
            }

            private long ReadInputHelper(P<byte> buf, long sz)
            {
                Utils.Assert(sz != 0);
                return ReadInputAsync(buf.mBuffer, buf.mOffset, checked((int)sz));
            }

            private void WriteOutputHelper(P<byte> buf, long sz)
            {
                WriteOutputAsync(buf.mBuffer, buf.mOffset, checked((int)sz));
                lock(mSyncObject)
                    mLowerBound += sz;
            }

            protected override void EncoderThreadLoop()
            {
                var settings = LZMA.CLzmaEncProps.LzmaEncProps_Init();
                var encoder = LZMA.LzmaEnc_Create(LZMA.ISzAlloc.SmallAlloc);
                var res = encoder.LzmaEnc_SetProps(settings);
                if(res != LZMA.SZ_OK)
                    throw new InvalidOperationException();

                mSettings = new byte[LZMA.LZMA_PROPS_SIZE];
                long binarySettingsSize = LZMA.LZMA_PROPS_SIZE;
                res = encoder.LzmaEnc_WriteProperties(mSettings, ref binarySettingsSize);
                if(res != LZMA.SZ_OK)
                    throw new InvalidOperationException();
                if(binarySettingsSize != LZMA.LZMA_PROPS_SIZE)
                    throw new NotSupportedException();

                res = encoder.LzmaEnc_Encode(mOutputHelper, mInputHelper, null, LZMA.ISzAlloc.SmallAlloc, LZMA.ISzAlloc.BigAlloc);
                if(res != LZMA.SZ_OK)
                    throw new InvalidOperationException();

                encoder.LzmaEnc_Destroy(LZMA.ISzAlloc.SmallAlloc, LZMA.ISzAlloc.BigAlloc);
            }

            internal override FileSet FinishFileSet(FileEntry[] entries, long inputSize, long outputSize)
            {
                return new FileSet {
                    Files = entries,
                    DataStream = new CoderStreamRef { CoderIndex = 0, StreamIndex = 0 },
                    InputStreams = new[] { new InputStream { Size = outputSize } },
                    Coders = new[] { new Coder {
                        MethodId = master._7zip.Legacy.CMethodId.kLzma,
                        Settings = mSettings,
                        InputStreams = new[] { new InputStreamRef { PackedStreamIndex = 0 } },
                        OutputStreams = new[] { new CoderStream { Size = inputSize } },
                    } },
                };
            }
        }

        public sealed class Lzma2Encoder: ThreadedEncoder
        {
            private long mLowerBound;
            private object mSyncObject;
            private LZMA.CSeqOutStream mOutputHelper;
            private LZMA.CSeqInStream mInputHelper;
            private int? mThreadCount;
            private byte mSettings;

            public Lzma2Encoder(int? threadCount)
            {
                mThreadCount = threadCount;
                mSyncObject = new object();
                mOutputHelper = new LZMA.CSeqOutStream(WriteOutputHelper);
                mInputHelper = new LZMA.CSeqInStream(ReadInputHelper);
                StartThread("LZMA 2 Stream Buffer Thread");
            }

            public override long LowerBound
            {
                get
                {
                    lock(mSyncObject)
                        return mLowerBound;
                }
            }

            public override long UpperBound
            {
                get { return CurrentInputSize; }
            }

            private long ReadInputHelper(P<byte> buf, long sz)
            {
                Utils.Assert(sz != 0);
                return ReadInputAsync(buf.mBuffer, buf.mOffset, checked((int)sz));
            }

            private void WriteOutputHelper(P<byte> buf, long sz)
            {
                WriteOutputAsync(buf.mBuffer, buf.mOffset, checked((int)sz));
                lock(mSyncObject)
                    mLowerBound += sz;
            }

            protected override void EncoderThreadLoop()
            {
                var settings = new LZMA.CLzma2EncProps();
                settings.Lzma2EncProps_Init();

                if(mThreadCount.HasValue)
                    settings.mNumBlockThreads = mThreadCount.Value;

                var encoder = new LZMA.CLzma2Enc(LZMA.ISzAlloc.SmallAlloc, LZMA.ISzAlloc.BigAlloc);
                var res = encoder.Lzma2Enc_SetProps(settings);
                if(res != LZMA.SZ_OK)
                    throw new InvalidOperationException();

                mSettings = encoder.Lzma2Enc_WriteProperties();

                res = encoder.Lzma2Enc_Encode(mOutputHelper, mInputHelper, null);
                if(res != LZMA.SZ_OK)
                    throw new InvalidOperationException();

                encoder.Lzma2Enc_Destroy();
            }

            internal override FileSet FinishFileSet(FileEntry[] entries, long inputSize, long outputSize)
            {
                return new FileSet {
                    Files = entries,
                    DataStream = new CoderStreamRef { CoderIndex = 0, StreamIndex = 0 },
                    InputStreams = new[] { new InputStream { Size = outputSize } },
                    Coders = new[] { new Coder {
                        MethodId = master._7zip.Legacy.CMethodId.kLzma2,
                        Settings = new byte[] { mSettings },
                        InputStreams = new[] { new InputStreamRef { PackedStreamIndex = 0 } },
                        OutputStreams = new[] { new CoderStream { Size = inputSize } },
                    } },
                };
            }
        }

        #endregion

        #region Constants & Variables

        private static readonly byte[] kSignature = { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C };

        private long mFileOrigin;
        private long mWrittenSync;
        private Stream mFileStream;
        private List<FileSet> mFileSets;
        private Encoder mEncoder; // TODO: rename to mEncoder

        #endregion

        #region Public Methods

        public ArchiveWriter(Stream stream)
        {
            if(stream == null)
                throw new ArgumentNullException("stream");

            if(!stream.CanWrite)
                throw new ArgumentException("Stream must support writing.", "stream");

            // Seeking is required only because we need to go back to the start of the stream
            // and fix up a reference to the header data. Possible solution: If we fix filesize
            // ahead of time we could reserve space for the header and output a fixed offset.
            if(!stream.CanSeek)
                throw new ArgumentException("Stream must support seeking.", "stream");

            // TODO: Implement the encoding independant of BinaryWriter endianess.
            if(!BitConverter.IsLittleEndian)
                throw new NotSupportedException("BinaryWriter must be little endian.");

            mFileStream = stream;
            mFileOrigin = stream.Position;

            var writer = new BinaryWriter(stream, Encoding.Unicode);

            writer.Write(kSignature);
            writer.Write((byte)0);
            writer.Write((byte)3);

            // We don't have a header yet so just write a placeholder. As a side effect
            // the placeholder will make this file look like a valid but empty archive.
            WriteHeaderInfo(writer, 0, 0, CRC.Finish(CRC.kInitCRC));

            mFileSets = new List<FileSet>();
            mWrittenSync = mFileStream.Position;
        }

        /// <summary>
        /// Returns the amount of data written so far. This is a lower bound for the
        /// current archive size if it would be closed now.
        /// </summary>
        public long WrittenSize
        {
            get
            {
                // NOTE: If we have an encoder we shouldn't access the file stream because it's owned by the encoder.

                long size = mWrittenSync;

                if(mEncoder != null)
                    size += mEncoder.LowerBound;

                return size;
            }
        }

        /// <summary>
        /// Returns an estimated limit for the file size if the archive would be closed now.
        /// This consists of WrittenSize plus an estimated size limit for buffered data and the header.
        /// The actual archive size may be smaller due to overestimating the header size.
        /// </summary>
        public long CurrentSizeLimit
        {
            get
            {
                // TODO: CalculateHeaderLimit does not include the metadata from the active encoder!
                long size = mWrittenSync + CalculateHeaderLimit();

                if(mEncoder != null)
                    size += mEncoder.UpperBound;

                return size;
            }
        }

        public void WriteFinalHeader()
        {
            if(mEncoder != null)
                mEncoder.Flush();

            var files = mFileSets.SelectMany(stream => stream.Files).ToArray();
            if(files.Length != 0)
            {
                long headerOffset = mFileStream.Position;

                var headerStream = new master._7zip.Legacy.CrcBuilderStream(mFileStream);
                var writer = new BinaryWriter(headerStream, Encoding.Unicode);

                WriteNumber(writer, BlockType.Header);

                var inputStreams = mFileSets.SelectMany(fileset => fileset.InputStreams).ToArray();
                if(inputStreams.Any(stream => stream.Size != 0))
                {
                    WriteNumber(writer, BlockType.MainStreamsInfo);
                    WriteNumber(writer, BlockType.PackInfo);
                    WriteNumber(writer, (ulong)0); // offset to input streams
                    WriteNumber(writer, inputStreams.Length);
                    WriteNumber(writer, BlockType.Size);
                    foreach(var stream in inputStreams)
                        WriteNumber(writer, stream.Size);
                    WriteNumber(writer, BlockType.End);
                    WriteNumber(writer, BlockType.UnpackInfo);
                    WriteNumber(writer, BlockType.Folder);
                    WriteNumber(writer, mFileSets.Count);
                    writer.Write((byte)0); // inline data
                    foreach(var fileset in mFileSets)
                    {
                        WriteNumber(writer, fileset.Coders.Length);
                        foreach(var coder in fileset.Coders)
                        {
                            int idlen = coder.MethodId.GetLength();
                            if(idlen >= 8)
                                throw new NotSupportedException();

                            int flags = idlen;

                            if(coder.InputStreams.Length != 1 || coder.OutputStreams.Length != 1)
                                flags |= 0x10;

                            if(coder.Settings != null)
                                flags |= 0x20;

                            writer.Write((byte)flags);

                            ulong id = coder.MethodId.Id;
                            for(int i = idlen - 1; i >= 0; i--)
                                writer.Write((byte)(id >> (i * 8)));

                            if((flags & 0x10) != 0)
                            {
                                WriteNumber(writer, coder.InputStreams.Length);
                                WriteNumber(writer, coder.OutputStreams.Length);
                            }

                            if((flags & 0x20) != 0)
                            {
                                WriteNumber(writer, coder.Settings.Length);
                                writer.Write(coder.Settings);
                            }

                            // TODO: Bind pairs and association to streams ...
                            if(fileset.Coders.Length > 1 || coder.InputStreams.Length != 1 || coder.OutputStreams.Length != 1)
                                throw new NotSupportedException();
                        }
                    }
                    WriteNumber(writer, BlockType.CodersUnpackSize);
                    foreach(var fileset in mFileSets)
                        WriteNumber(writer, fileset.DataStream.GetSize(fileset));
                    WriteNumber(writer, BlockType.End);
                    WriteNumber(writer, BlockType.SubStreamsInfo);
                    WriteNumber(writer, BlockType.NumUnpackStream);
                    foreach(var stream in mFileSets)
                        WriteNumber(writer, stream.Files.Length);
                    WriteNumber(writer, BlockType.Size);
                    foreach(var stream in mFileSets)
                        for(int i = 0; i < stream.Files.Length - 1; i++)
                            WriteNumber(writer, stream.Files[i].Size);
                    WriteNumber(writer, BlockType.End);
                    WriteNumber(writer, BlockType.End);
                }

                WriteNumber(writer, BlockType.FilesInfo);
                WriteNumber(writer, files.Length);

                WriteNumber(writer, BlockType.Name);
                WriteNumber(writer, 1 + files.Sum(file => file.Name.Length + 1) * 2);
                writer.Write((byte)0); // inline names
                for(int i = 0; i < files.Length; i++)
                {
                    string name = files[i].Name;
                    for(int j = 0; j < name.Length; j++)
                        writer.Write(name[j]);
                    writer.Write('\0');
                }

                /* had to disable empty streams and files because above BlockType.Size doesn't respect them
                 * if a file is marked as empty stream it doesn't get a size/hash entry in the coder header above
                 * however, to fix that, we'd need to skip coders with only empty files too, so its easier to do it this way for now
                if(files.Any(file => file.Size == 0))
                {
                    int emptyStreams = 0;

                    WriteNumber(writer, BlockType.EmptyStream);
                    WriteNumber(writer, (files.Length + 7) / 8);
                    for(int i = 0; i < files.Length; i += 8)
                    {
                        int mask = 0;
                        for(int j = 0; j < 8; j++)
                        {
                            if(i + j < files.Length && files[i + j].Size == 0)
                            {
                                mask |= 1 << (7 - j);
                                emptyStreams++;
                            }
                        }
                        writer.Write((byte)mask);
                    }

                    WriteNumber(writer, BlockType.EmptyFile);
                    WriteNumber(writer, (emptyStreams + 7) / 8);
                    for(int i = 0; i < emptyStreams; i += 8)
                    {
                        int mask = 0;
                        for(int j = 0; j < 8; j++)
                            if(i + j < emptyStreams)
                                mask |= 1 << (7 - j);
                        writer.Write((byte)mask);
                    }
                }
                */

                int ctimeCount = files.Count(file => file.CTime.HasValue);
                if(ctimeCount != 0)
                {
                    WriteNumber(writer, BlockType.CTime);

                    if(ctimeCount == files.Length)
                    {
                        WriteNumber(writer, 2 + ctimeCount * 8);
                        writer.Write((byte)1);
                    }
                    else
                    {
                        WriteNumber(writer, (ctimeCount + 7) / 8 + 2 + ctimeCount * 8);
                        writer.Write((byte)0);

                        for(int i = 0; i < files.Length; i += 8)
                        {
                            int mask = 0;
                            for(int j = 0; j < 8; j++)
                                if(i + j < files.Length && files[i + j].CTime.HasValue)
                                    mask |= 1 << (7 - j);

                            writer.Write((byte)mask);
                        }
                    }

                    writer.Write((byte)0); // inline data

                    for(int i = 0; i < files.Length; i++)
                        if(files[i].CTime.HasValue)
                            writer.Write(files[i].CTime.Value.ToFileTimeUtc());
                }

                int atimeCount = files.Count(file => file.ATime.HasValue);
                if(atimeCount != 0)
                {
                    WriteNumber(writer, BlockType.ATime);

                    if(atimeCount == files.Length)
                    {
                        WriteNumber(writer, 2 + atimeCount * 8);
                        writer.Write((byte)1);
                    }
                    else
                    {
                        WriteNumber(writer, (atimeCount + 7) / 8 + 2 + atimeCount * 8);
                        writer.Write((byte)0);

                        for(int i = 0; i < files.Length; i += 8)
                        {
                            int mask = 0;
                            for(int j = 0; j < 8; j++)
                                if(i + j < files.Length && files[i + j].ATime.HasValue)
                                    mask |= 1 << (7 - j);

                            writer.Write((byte)mask);
                        }
                    }

                    writer.Write((byte)0); // inline data

                    for(int i = 0; i < files.Length; i++)
                        if(files[i].ATime.HasValue)
                            writer.Write(files[i].ATime.Value.ToFileTimeUtc());
                }

                int mtimeCount = files.Count(file => file.MTime.HasValue);
                if(mtimeCount != 0)
                {
                    WriteNumber(writer, BlockType.MTime);

                    if(mtimeCount == files.Length)
                    {
                        WriteNumber(writer, 2 + mtimeCount * 8);
                        writer.Write((byte)1);
                    }
                    else
                    {
                        WriteNumber(writer, (mtimeCount + 7) / 8 + 2 + mtimeCount * 8);
                        writer.Write((byte)0);

                        for(int i = 0; i < files.Length; i += 8)
                        {
                            int mask = 0;
                            for(int j = 0; j < 8; j++)
                                if(i + j < files.Length && files[i + j].MTime.HasValue)
                                    mask |= 1 << (7 - j);

                            writer.Write((byte)mask);
                        }
                    }

                    writer.Write((byte)0); // inline data

                    for(int i = 0; i < files.Length; i++)
                        if(files[i].MTime.HasValue)
                            writer.Write(files[i].MTime.Value.ToFileTimeUtc());
                }

                WriteNumber(writer, BlockType.End);

                uint headerCRC = headerStream.Finish();
                long headerSize = mFileStream.Position - headerOffset;
                mFileStream.Position = mFileOrigin + 8;
                WriteHeaderInfo(new BinaryWriter(mFileStream, Encoding.Unicode), headerOffset - mFileOrigin - 0x20, headerSize, headerCRC);
            }

            mFileStream.Close(); // so we don't start overwriting stuff accidently by calling more functions
        }

        #endregion

        #region Internal Methods

        // TODO: move into Encoder class?
        internal void Encoder_FinishFileSet(FileSet fileset)
        {
            mFileSets.Add(fileset);
            mWrittenSync = mFileStream.Position;
        }

        // TODO: move into Encoder class?
        internal void Encoder_WriteFileSet(BufferedFileSet fileset)
        {
            fileset.Buffer.FullCopyTo(mFileStream);
            Encoder_FinishFileSet(fileset.Metadata);
        }

        #endregion

        #region Encoding Methods

        /// <summary>
        /// Returns the currently connected encoder.
        /// </summary>
        public Encoder CurrentEncoder
        {
            get { return mEncoder; }
        }

        /// <summary>
        /// Flushes the currently connected encoder and disconnects it.
        /// </summary>
        public void DisconnectEncoder()
        {
            if(mEncoder != null)
            {
                mEncoder.Disconnect();

                Debug.Assert(mEncoder == null);
            }
        }

        /// <summary>
        /// Flushes the currently connected encoder and selects the given encoder as current.
        /// The connected encoder can write directly to the archive file with reduced buffering.
        /// </summary>
        public void ConnectEncoder(Encoder encoder)
        {
            if(encoder == null)
                throw new ArgumentNullException("encoder");

            if(mEncoder == encoder)
            {
                mEncoder.Flush();
            }
            else
            {
                if(encoder.IsConnected)
                    throw new InvalidOperationException("The given encoder is already connected to another ArchiveWriter.");

                if(mEncoder != null)
                    mEncoder.Disconnect();

                mEncoder = encoder;
                mEncoder.Connect(this);
            }
        }

        #endregion

        #region Private Helper Methods

        private void WriteHeaderInfo(BinaryWriter writer, long offset, long size, uint crc)
        {
            uint infoCRC = CRC.kInitCRC;
            infoCRC = CRC.Update(infoCRC, offset);
            infoCRC = CRC.Update(infoCRC, size);
            infoCRC = CRC.Update(infoCRC, crc);
            infoCRC = CRC.Finish(infoCRC);

            writer.Write(infoCRC);
            writer.Write(offset);
            writer.Write(size);
            writer.Write(crc);
        }

        private void WriteNumber(BinaryWriter writer, BlockType value)
        {
            WriteNumber(writer, (byte)value);
        }

        private void WriteNumber(BinaryWriter writer, long number)
        {
            WriteNumber(writer, checked((ulong)number));
        }

        private void WriteNumber(BinaryWriter writer, ulong number)
        {
            // TODO: Use the short forms if applicable.
            writer.Write((byte)0xFF);
            writer.Write(number);
        }

        private long CalculateHeaderLimit()
        {
            return 1024; // HACK: mFileSets.SelectMany(stream => stream.Files).ToArray() is too slow

            //const int kMaxNumberLen = 9; // 0xFF + sizeof(ulong)
            //const int kBlockTypeLen = kMaxNumberLen;
            //const int kZeroNumberLen = kMaxNumberLen;
            //long limit = 0;

            //var files = mFileSets.SelectMany(stream => stream.Files).ToArray();
            //if(files.Length != 0)
            //{
            //    limit += kBlockTypeLen; // BlockType.Header

            //    var inputStreams = mFileSets.SelectMany(fileset => fileset.InputStreams).ToArray();
            //    if(inputStreams.Any(stream => stream.Size != 0))
            //    {
            //        limit += kBlockTypeLen; // BlockType.MainStreamsInfo
            //        limit += kBlockTypeLen; // BlockType.PackInfo
            //        limit += kZeroNumberLen; // zero = offset to input streams
            //        limit += kMaxNumberLen; // inputStreams.Length
            //        limit += kBlockTypeLen; //BlockType.Size
            //        limit += inputStreams.Length * kMaxNumberLen; // inputStreams: inputStream.Size
            //        limit += kBlockTypeLen; // BlockType.End
            //        limit += kBlockTypeLen; // BlockType.UnpackInfo
            //        limit += kBlockTypeLen; // BlockType.Folder
            //        limit += kMaxNumberLen; // mFileSets.Count
            //        limit += kZeroNumberLen; // zero = inline data
            //        foreach(var fileset in mFileSets)
            //        {
            //            limit += kMaxNumberLen; // fileset.Coders.Length
            //            foreach(var coder in fileset.Coders)
            //            {
            //                limit += 1; // flags
            //                limit += coder.MethodId.GetLength(); // coder.MethodId

            //                if(coder.InputStreams.Length != 1 || coder.OutputStreams.Length != 1)
            //                {
            //                    limit += kMaxNumberLen; // coder.InputStreams.Length
            //                    limit += kMaxNumberLen; // coder.OutputStreams.Length
            //                }

            //                if(coder.Settings != null)
            //                {
            //                    limit += kMaxNumberLen; // coder.Settings.Length
            //                    limit += coder.Settings.Length; // coder.Settings
            //                }

            //                // TODO: Bind pairs and association to streams ...
            //                if(fileset.Coders.Length > 1 || coder.InputStreams.Length != 1 || coder.OutputStreams.Length != 1)
            //                    throw new NotSupportedException();
            //            }
            //        }
            //        limit += kBlockTypeLen; // BlockType.CodersUnpackSize
            //        limit += mFileSets.Count * kMaxNumberLen; // mFileSets: fileset.DataStream.GetSize(fileset)
            //        limit += kBlockTypeLen; // BlockType.End
            //        limit += kBlockTypeLen; // BlockType.SubStreamsInfo
            //        limit += kBlockTypeLen; // BlockType.NumUnpackStream
            //        limit += mFileSets.Count * kMaxNumberLen; // mFileSets: stream.Files.Length
            //        limit += kBlockTypeLen; // BlockType.Size
            //        limit += mFileSets.Sum(fileset => fileset.Files.Length - 1) * kMaxNumberLen; // mFileSets: fileset.Files[0..n-1]: stream.Files[i].Size
            //        limit += kBlockTypeLen; // BlockType.End
            //        limit += kBlockTypeLen; // BlockType.End
            //    }

            //    limit += kBlockTypeLen; // BlockType.FilesInfo
            //    limit += kMaxNumberLen; // files.Length

            //    limit += kBlockTypeLen; // BlockType.Name
            //    limit += kMaxNumberLen; // 1 + files.Sum(file => file.Name.Length + 1) * 2
            //    limit += kZeroNumberLen; // zero = inline names
            //    for(int i = 0; i < files.Length; i++)
            //        limit += (files[i].Name.Length + 1) * 2;

            //    if(files.Any(file => file.Size == 0))
            //    {
            //        limit += kBlockTypeLen; // BlockType.EmptyStream
            //        limit += kMaxNumberLen; // (files.Length + 7) / 8
            //        limit += (files.Length + 7) / 8; // bit vector
            //        limit += kBlockTypeLen; // BlockType.EmptyFile
            //        limit += kMaxNumberLen; // (files.Length + 7) / 8 -- this is an upper bound, for an exact size we need to count the number of empty streams
            //        limit += (files.Length + 7) / 8; // bit vector
            //    }

            //    limit += kBlockTypeLen; // BlockType.CTime
            //    limit += kMaxNumberLen; // (ctimeCount + 7) / 8 + 2 + ctimeCount * 8;
            //    limit += (files.Length + 7) / 8 + 2 + files.Length * 8;

            //    limit += kBlockTypeLen; // BlockType.ATime
            //    limit += kMaxNumberLen; // (atimeCount + 7) / 8 + 2 + atimeCount * 8;
            //    limit += (files.Length + 7) / 8 + 2 + files.Length * 8;

            //    limit += kBlockTypeLen; // BlockType.MTime
            //    limit += kMaxNumberLen; // (mtimeCount + 7) / 8 + 2 + mtimeCount * 8;
            //    limit += (files.Length + 7) / 8 + 2 + files.Length * 8;

            //    limit += kBlockTypeLen; // BlockType.End
            //}

            //return limit;
        }

        #endregion
    }

    public static class ArchiveWriterExtensions
    {
        #region ArchiveWriter Extensions

        public static void WriteFile(this ArchiveWriter writer, IArchiveWriterEntry metadata, Stream content)
        {
            if(writer.CurrentEncoder == null)
                throw new InvalidOperationException("No current encoder configured.");

            using(var stream = writer.CurrentEncoder.BeginWriteFile(metadata))
                content.CopyTo(stream);
        }

        public static void WriteFile(this ArchiveWriter writer, DirectoryInfo root, FileInfo file)
        {
            using(var content = file.OpenRead())
                writer.WriteFile(new FileBasedArchiveWriterEntry(root, file), content);
        }

        public static void WriteFiles(this ArchiveWriter writer, DirectoryInfo root, IEnumerable<FileInfo> files)
        {
            foreach(var file in files)
                writer.WriteFile(root, file);
        }

        public static void WriteFiles(this ArchiveWriter writer, DirectoryInfo root, params FileInfo[] files)
        {
            writer.WriteFiles(root, (IEnumerable<FileInfo>)files);
        }

        #endregion
    }
}
