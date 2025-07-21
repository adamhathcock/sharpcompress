using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SharpCompress.IO
{
    public interface IStreamStack
    {
        /// <summary>
        /// Gets or sets the default buffer size to be applied when buffering is enabled for this stream stack.
        /// This value is used by the SetBuffer extension method to configure buffering on the appropriate stream
        /// in the stack hierarchy. A value of 0 indicates no default buffer size is set.
        /// </summary>
        int DefaultBufferSize { get; set; }

        /// <summary>
        /// Returns the immediate underlying stream in the stack.
        /// </summary>
        Stream BaseStream();

        /// <summary>
        /// Gets or sets the size of the buffer if the stream supports buffering; otherwise, returns 0.
        /// This property must not throw.
        /// </summary>
        int BufferSize { get; set; }

        /// <summary>
        /// Gets or sets the current position within the buffer if the stream supports buffering; otherwise, returns 0.
        /// This property must not throw.
        /// </summary>
        int BufferPosition { get; set; }

        /// <summary>
        /// Updates the internal position state of the stream. This should not perform seeking on the underlying stream,
        /// but should update any internal position or buffer state as appropriate for the stream implementation.
        /// </summary>
        /// <param name="position">The absolute position to set within the stream stack.</param>
        void SetPosition(long position);

#if DEBUG_STREAMS
        /// <summary>
        /// Gets or sets the unique instance identifier for debugging purposes.
        /// </summary>
        long InstanceId { get; set; }
#endif
    }

    internal static class StackStreamExtensions
    {
        /// <summary>
        /// Gets the logical position of the first buffering stream in the stack, or 0 if none exist.
        /// </summary>
        /// <param name="stream">The most derived (outermost) stream in the stack.</param>
        /// <returns>The position of the first buffering stream, or 0 if not found.</returns>
        internal static long GetPosition(this IStreamStack stream)
        {
            IStreamStack? current = stream;

            while (current != null)
            {
                if (current.BufferSize != 0 && current is Stream st)
                {
                    return st.Position;
                }
                current = current?.BaseStream() as IStreamStack;
            }
            return 0;
        }

        /// <summary>
        /// Rewinds the buffer of the outermost buffering stream in the stack by the specified count, if supported.
        /// Only the most derived buffering stream is affected.
        /// </summary>
        /// <param name="stream">The most derived (outermost) stream in the stack.</param>
        /// <param name="count">The number of bytes to rewind within the buffer.</param>
        internal static void Rewind(this IStreamStack stream, int count)
        {
            Stream baseStream = stream.BaseStream();
            Stream thisStream = (Stream)stream;
            IStreamStack? buffStream = null;
            IStreamStack? current = stream;

            while (buffStream == null && current != null)
            {
                if (current.BufferSize != 0)
                {
                    buffStream = current;
                    buffStream.BufferPosition -= Math.Min(buffStream.BufferPosition, count);
                }
                current = current?.BaseStream() as IStreamStack;
            }
        }

        /// <summary>
        /// Sets the buffer size on the first buffering stream in the stack, or on the outermost stream if none exist.
        /// If <paramref name="force"/> is true, sets the buffer size regardless of current value.
        /// </summary>
        /// <param name="stream">The most derived (outermost) stream in the stack.</param>
        /// <param name="bufferSize">The buffer size to set.</param>
        /// <param name="force">If true, forces the buffer size to be set even if already set.</param>
        internal static void SetBuffer(this IStreamStack stream, int bufferSize, bool force)
        {
            if (bufferSize == 0 || stream == null)
                return;

            IStreamStack? current = stream;
            IStreamStack defaultBuffer = stream;
            IStreamStack? buffer = null;

            // First pass: find the deepest IStreamStack
            while (current != null)
            {
                defaultBuffer = current;
                if (buffer == null && ((current.BufferSize != 0 && bufferSize != 0) || force))
                    buffer = current;
                if (defaultBuffer.DefaultBufferSize != 0)
                    break;
                current = current.BaseStream() as IStreamStack;
            }
            if (defaultBuffer.DefaultBufferSize == 0)
                defaultBuffer.DefaultBufferSize = bufferSize;
            (buffer ?? stream).BufferSize = bufferSize;
        }

        /// <summary>
        /// Attempts to set the position in the stream stack. If a buffering stream is present and the position is within its buffer,
        /// BufferPosition is set on the outermost buffering stream and all intermediate streams update their internal state via SetPosition.
        /// If no buffering stream is present, seeks as close to the root stream as possible and updates all intermediate streams' state via SetPosition.
        /// Seeking is never performed if any intermediate stream in the stack is buffering.
        /// Throws if the position cannot be set.
        /// </summary>
        /// <param name="stream">
        /// The most derived (outermost) stream in the stack. The method traverses up the stack via BaseStream() until a stream can satisfy the buffer or seek request.
        /// </param>
        /// <param name="position">The absolute position to set.</param>
        /// <returns>The position that was set.</returns>
        internal static long StackSeek(this IStreamStack stream, long position)
        {
            var stack = new List<IStreamStack>();
            Stream? current = stream as Stream;
            int lastBufferingIndex = -1;
            int firstSeekableIndex = -1;
            Stream? firstSeekableStream = null;

            // Traverse the stack, collecting info
            while (current is IStreamStack stackStream)
            {
                stack.Add(stackStream);
                if (stackStream.BufferSize > 0)
                {
                    lastBufferingIndex = stack.Count - 1;
                    break;
                }
                current = stackStream.BaseStream();
            }

            // Find the first seekable stream (closest to the root)
            if (current != null && current.CanSeek)
            {
                firstSeekableIndex = stack.Count;
                firstSeekableStream = current;
            }

            // If any buffering stream exists, try to set BufferPosition on the outermost one
            if (lastBufferingIndex != -1)
            {
                var bufferingStream = stack[lastBufferingIndex];
                if (position >= 0 && position < bufferingStream.BufferSize)
                {
                    bufferingStream.BufferPosition = (int)position;
                    return position;
                }
                else
                {
                    // If position is not in buffer, reset buffer and proceed as non-buffering
                    bufferingStream.BufferPosition = 0;
                }
                // Continue to seek as if no buffer is present
            }

            // If no buffering, or buffer was reset, seek at the first seekable stream (closest to the root)
            if (firstSeekableStream != null)
            {
                firstSeekableStream.Seek(position, SeekOrigin.Begin);
                return firstSeekableStream.Position;
            }

            throw new NotSupportedException(
                "Cannot set position on this stream stack (no seekable or buffering stream supports the requested position)."
            );
        }

        /// <summary>
        /// Reads bytes from the stream, using the position to observe how much was actually consumed and rewind the buffer to ensure further reads are correct.
        /// This is required to prevent buffered reads from skipping data, while also benefiting from buffering and reduced stream IO reads.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer to read data into.</param>
        /// <param name="offset">The offset in the buffer to start writing data.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <param name="buffStream">Returns the buffering stream found in the stack, or null if none exists.</param>
        /// <param name="baseReadCount">Returns the number of bytes actually read from the base stream, or -1 if no buffering stream was found.</param>
        /// <returns>The number of bytes read into the buffer.</returns>
        internal static int Read(
            this IStreamStack stream,
            byte[] buffer,
            int offset,
            int count,
            out IStreamStack? buffStream,
            out int baseReadCount
        )
        {
            Stream baseStream = stream.BaseStream();
            Stream thisStream = (Stream)stream;
            IStreamStack? current = stream;
            buffStream = null;
            baseReadCount = -1;

            while (buffStream == null && (current = current?.BaseStream() as IStreamStack) != null)
            {
                if (current.BufferSize != 0)
                {
                    buffStream = current;
                }
            }

            long buffPos = buffStream == null ? -1 : ((Stream)buffStream).Position;

            int read = baseStream.Read(buffer, offset, count); //amount read in to buffer

            if (buffPos != -1)
            {
                baseReadCount = (int)(((Stream)buffStream!).Position - buffPos);
            }
            return read;
        }

#if DEBUG_STREAMS
        private static long _instanceCounter = 0;

        private static string cleansePos(long pos)
        {
            if (pos < 0)
                return "";
            return "Px" + pos.ToString("x");
        }

        /// <summary>
        /// Gets or creates a unique instance ID for the stream stack for debugging purposes.
        /// </summary>
        /// <param name="stream">The stream stack.</param>
        /// <param name="instanceId">Reference to the instance ID field.</param>
        /// <param name="construct">Whether this is being called during construction.</param>
        /// <returns>The instance ID.</returns>
        public static long GetInstanceId(
            this IStreamStack stream,
            ref long instanceId,
            bool construct
        )
        {
            if (instanceId == 0) //will not be equal to 0 when inherited IStackStream types are being used
                instanceId = System.Threading.Interlocked.Increment(ref _instanceCounter);
            return instanceId;
        }

        /// <summary>
        /// Writes a debug message for stream construction.
        /// </summary>
        /// <param name="stream">The stream stack.</param>
        /// <param name="constructing">The type being constructed.</param>
        public static void DebugConstruct(this IStreamStack stream, Type constructing)
        {
            long id = stream.InstanceId;
            stream.InstanceId = GetInstanceId(stream, ref id, true);
            var frame = (new StackTrace()).GetFrame(3);
            string parentInfo =
                frame != null
                    ? $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}()"
                    : "Unknown";
            if (constructing.FullName == stream.GetType().FullName) //don't debug base IStackStream types
                Debug.WriteLine(
                    $"{GetStreamStackString(stream, true)} : Constructed by [{parentInfo}]"
                );
        }

        /// <summary>
        /// Writes a debug message for stream disposal.
        /// </summary>
        /// <param name="stream">The stream stack.</param>
        /// <param name="constructing">The type being disposed.</param>
        public static void DebugDispose(this IStreamStack stream, Type constructing)
        {
            var frame = (new StackTrace()).GetFrame(3);
            string parentInfo =
                frame != null
                    ? $"{frame.GetMethod()?.DeclaringType?.Name}.{frame.GetMethod()?.Name}()"
                    : "Unknown";
            if (constructing.FullName == stream.GetType().FullName) //don't debug base IStackStream types
                Debug.WriteLine($"{GetStreamStackString(stream, false)} : Disposed by [{parentInfo}]");
        }

        /// <summary>
        /// Writes a debug trace message for the stream.
        /// </summary>
        /// <param name="stream">The stream stack.</param>
        /// <param name="message">The debug message to write.</param>
        public static void DebugTrace(this IStreamStack stream, string message)
        {
            Debug.WriteLine(
                $"{GetStreamStackString(stream, false)} : [{stream.GetType().Name}]{message}"
            );
        }

        /// <summary>
        /// Returns the full stream chain as a string, including instance IDs and positions.
        /// </summary>
        /// <param name="stream">The stream stack to represent.</param>
        /// <param name="construct">Whether this is being called during construction.</param>
        /// <returns>A string representation of the entire stream stack.</returns>
        public static string GetStreamStackString(this IStreamStack stream, bool construct)
        {
            var sb = new StringBuilder();
            Stream? current = stream as Stream;
            while (current != null)
            {
                IStreamStack? sStack = current as IStreamStack;
                string id = sStack != null ? "#" + sStack.InstanceId.ToString() : "";
                string buffSize = sStack != null ? "Bx" + sStack.BufferSize.ToString("x") : "";
                string defBuffSize =
                    sStack != null ? "Dx" + sStack.DefaultBufferSize.ToString("x") : "";

                if (sb.Length > 0)
                    sb.Insert(0, "/");
                try
                {
                    sb.Insert(
                        0,
                        $"{current.GetType().Name}{id}[{cleansePos(current.Position)}:{buffSize}:{defBuffSize}]"
                    );
                }
                catch
                {
                    if (current is SharpCompressStream scs)
                        sb.Insert(
                            0,
                            $"{current.GetType().Name}{id}[{cleansePos(scs.InternalPosition)}:{buffSize}:{defBuffSize}]"
                        );
                    else
                        sb.Insert(0, $"{current.GetType().Name}{id}[:{buffSize}]");
                }
                if (sStack != null)
                    current = sStack.BaseStream(); //current may not be a IStreamStack, allow one more loop
                else
                    break;
            }
            return sb.ToString();
        }
#endif
    }
}
