using System.IO;

namespace SharpCompress.IO;

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
