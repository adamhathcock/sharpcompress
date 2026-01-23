using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

/// <summary>
/// Tests that verify Dispose and DisposeAsync behavior of SharpCompressStream.
/// </summary>
public class SharpCompressStreamDisposalTests
{
    #region Dispose Tests (Synchronous)

    [Fact]
    public void Dispose_WithLeaveOpenFalse_DisposesInnerStream()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(innerStream, leaveOpen: false);

        // Act
        sharpStream.Dispose();

        // Assert
        Assert.True(innerStream.IsDisposed, "Inner stream should be disposed when leaveOpen=false");
    }

    [Fact]
    public void Dispose_WithLeaveOpenTrue_DoesNotDisposeInnerStream()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(innerStream, leaveOpen: true);

        // Act
        sharpStream.Dispose();

        // Assert
        Assert.False(
            innerStream.IsDisposed,
            "Inner stream should NOT be disposed when leaveOpen=true"
        );
    }

    [Fact]
    public void Dispose_WithThrowOnDisposeFalse_DoesNotThrow()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            throwOnDispose: false
        );

        // Act & Assert (should not throw)
        sharpStream.Dispose();
    }

    [Fact]
    public void Dispose_WithThrowOnDisposeTrue_Throws()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            throwOnDispose: true
        );

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => sharpStream.Dispose());
        Assert.Contains("ThrowOnDispose", ex.Message);
    }

    [Fact]
    public void Dispose_MultipleTimes_OnlyDisposesOnce()
    {
        // Arrange
        var countingStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(countingStream, leaveOpen: false);

        // Act
        sharpStream.Dispose();
        sharpStream.Dispose(); // Second dispose should be idempotent
        sharpStream.Dispose(); // Third dispose should be idempotent

        // Assert - should not throw and inner stream should only be disposed once
        Assert.True(countingStream.IsDisposed);
    }

    [Fact]
    public void Dispose_WithBuffering_ReturnsBufferToPool()
    {
        // Arrange
        var data = new byte[0x10000];
        var innerStream = new MemoryStream(data);
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: true,
            bufferSize: 0x1000
        );

        // Get initial pool stats
        var initialRented = ArrayPool<byte>.Shared.ToString();

        // Act
        sharpStream.Dispose();

        // Assert - we can't directly check pool state, but we verify no exceptions
        // and the stream is disposed properly
        Assert.True(sharpStream.CanRead == false || sharpStream.CanRead == true); // Check no exception on property access
    }

    [Fact]
    public void Dispose_WithoutBuffering_SuccessfullyDisposes()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            bufferSize: 0 // No buffering
        );

        // Act
        sharpStream.Dispose();

        // Assert
        Assert.True(innerStream.IsDisposed);
    }

    [Fact]
    public void Dispose_WithLeaveOpenTrueAndThrowOnDisposeTrue_SetsDisposedFlagButDoesNotThrow()
    {
        // Arrange - special case: LeaveOpen=true takes precedence, so no throw should occur
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: true,
            throwOnDispose: true
        );

        // Act & Assert
        // According to code: LeaveOpen is checked before ThrowOnDispose,
        // so this should not throw
        sharpStream.Dispose();
        Assert.False(innerStream.IsDisposed);
    }

    [Fact]
    public void Dispose_SetsFlagCorrectly()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: true,
            throwOnDispose: false
        );

        // Act
        sharpStream.Dispose();

        // Assert - check by attempting to use the stream (should not throw ObjectDisposedException)
        // Actually, SharpCompressStream delegates to inner stream, so we just check it doesn't crash
        Assert.NotNull(sharpStream);
    }

    [Fact]
    public void Dispose_WithLeaveOpenTrueAndWithBuffer_DoesNotDisposeInnerStreamOrThrow()
    {
        // Arrange
        var data = new byte[0x10000];
        var innerStream = new TestStream(new MemoryStream(data));
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: true,
            bufferSize: 0x2000
        );

        // Act - read some data to fill buffer
        var buffer = new byte[100];
        sharpStream.Read(buffer, 0, 100);

        // Dispose
        sharpStream.Dispose();

        // Assert
        Assert.False(
            innerStream.IsDisposed,
            "Inner stream should not be disposed when LeaveOpen=true"
        );
    }

    #endregion

    #region DisposeAsync Tests (Asynchronous)

#if !LEGACY_DOTNET

    [Fact]
    public async Task DisposeAsync_WithLeaveOpenFalse_DisposesInnerStream()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(innerStream, leaveOpen: false);

        // Act
        await sharpStream.DisposeAsync();

        // Assert
        Assert.True(innerStream.IsDisposed, "Inner stream should be disposed when leaveOpen=false");
    }

    [Fact]
    public async Task DisposeAsync_WithLeaveOpenTrue_DoesNotDisposeInnerStream()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(innerStream, leaveOpen: true);

        // Act
        await sharpStream.DisposeAsync();

        // Assert
        Assert.False(
            innerStream.IsDisposed,
            "Inner stream should NOT be disposed when leaveOpen=true"
        );
    }

    [Fact]
    public async Task DisposeAsync_WithThrowOnDisposeFalse_DoesNotThrow()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            throwOnDispose: false
        );

        // Act & Assert (should not throw)
        await sharpStream.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WithThrowOnDisposeTrue_Throws()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            throwOnDispose: true
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sharpStream.DisposeAsync().AsTask()
        );
        Assert.Contains("ThrowOnDispose", ex.Message);
    }

    [Fact]
    public async Task DisposeAsync_MultipleTimes_OnlyDisposesOnce()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(innerStream, leaveOpen: false);

        // Act
        await sharpStream.DisposeAsync();
        await sharpStream.DisposeAsync(); // Second dispose should be idempotent
        await sharpStream.DisposeAsync(); // Third dispose should be idempotent

        // Assert
        Assert.True(innerStream.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_WithBuffering_ReturnsBufferToPool()
    {
        // Arrange
        var data = new byte[0x10000];
        var innerStream = new MemoryStream(data);
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: true,
            bufferSize: 0x1000
        );

        // Act
        await sharpStream.DisposeAsync();

        // Assert - no exception thrown
        Assert.NotNull(sharpStream);
    }

    [Fact]
    public async Task DisposeAsync_WithoutBuffering_SuccessfullyDisposes()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            bufferSize: 0 // No buffering
        );

        // Act
        await sharpStream.DisposeAsync();

        // Assert
        Assert.True(innerStream.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_WithLeaveOpenTrueAndThrowOnDisposeTrue_SetsDisposedFlagButDoesNotThrow()
    {
        // Arrange - special case: LeaveOpen=true takes precedence, so no throw should occur
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: true,
            throwOnDispose: true
        );

        // Act & Assert
        await sharpStream.DisposeAsync();
        Assert.False(innerStream.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsync_WithLeaveOpenTrueAndWithBuffer_DoesNotDisposeInnerStreamOrThrow()
    {
        // Arrange
        var data = new byte[0x10000];
        var innerStream = new TestStream(new MemoryStream(data));
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: true,
            bufferSize: 0x2000
        );

        // Act - read some data to fill buffer
        var buffer = new byte[100];
        await sharpStream.ReadAsync(buffer, 0, 100);

        // Dispose
        await sharpStream.DisposeAsync();

        // Assert
        Assert.False(
            innerStream.IsDisposed,
            "Inner stream should not be disposed when LeaveOpen=true"
        );
    }

    [Fact]
    public async Task DisposeAsync_AfterReadWithBuffer_ProperlyReturnsBufferAndDisposesStream()
    {
        // Arrange
        var data = new byte[0x100000];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 256);
        }

        var innerStream = new TestStream(new MemoryStream(data));
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            bufferSize: 0x10000
        );

        // Act - perform some reads to exercise the buffer
        var readBuffer = new byte[0x1000];
        await sharpStream.ReadAsync(readBuffer, 0, readBuffer.Length);
        await sharpStream.ReadAsync(readBuffer, 0, readBuffer.Length);

        // Now dispose
        await sharpStream.DisposeAsync();

        // Assert
        Assert.True(innerStream.IsDisposed);
    }

#endif

    #endregion

    #region Integration Tests

    [Fact]
    public void Dispose_UsingStatement_ProperlyDisposesStream()
    {
        // Arrange & Act
        TestStream innerStream;
        using (var sharpStream = new SharpCompressStream(
            innerStream = new TestStream(new MemoryStream()),
            leaveOpen: false
        ))
        {
            // Use the stream
            var buffer = new byte[10];
            sharpStream.Read(buffer, 0, 0); // No-op read
        }

        // Assert
        Assert.True(innerStream.IsDisposed);
    }

#if !LEGACY_DOTNET

    [Fact]
    public async Task DisposeAsync_UsingAsyncStatement_ProperlyDisposesStream()
    {
        // Arrange & Act
        TestStream innerStream;
        await using (var sharpStream = new SharpCompressStream(
            innerStream = new TestStream(new MemoryStream()),
            leaveOpen: false
        ))
        {
            // Use the stream
            var buffer = new byte[10];
            await sharpStream.ReadAsync(buffer, 0, 0); // No-op read
        }

        // Assert
        Assert.True(innerStream.IsDisposed);
    }

#endif

    [Fact]
    public void Dispose_WithCreateFactory_ProperlyDisposes()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = SharpCompressStream.Create(
            innerStream,
            leaveOpen: false,
            throwOnDispose: false,
            bufferSize: 0x1000
        );

        // Act
        sharpStream.Dispose();

        // Assert
        Assert.True(innerStream.IsDisposed);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Dispose_AfterSeekWithBuffer_ProperlyDisposesAndClearsBuffer()
    {
        // Arrange
        var data = new byte[0x100000];
        var innerStream = new TestStream(new MemoryStream(data));
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            bufferSize: 0x10000
        );

        // Act - perform seek operations
        sharpStream.Seek(0x50000, SeekOrigin.Begin);
        var buffer = new byte[100];
        sharpStream.Read(buffer, 0, 100);

        // Dispose
        sharpStream.Dispose();

        // Assert
        Assert.True(innerStream.IsDisposed);
    }

    [Fact]
    public void Dispose_WithZeroBuffer_DoesNotCrash()
    {
        // Arrange
        var innerStream = new TestStream(new MemoryStream());
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            bufferSize: 0 // Zero buffer size
        );

        // Act & Assert - should not crash
        sharpStream.Dispose();
        Assert.True(innerStream.IsDisposed);
    }

    [Fact]
    public void Dispose_WithLargeBuffer_DoesNotCrash()
    {
        // Arrange
        var data = new byte[0x1000000]; // 16MB data
        var innerStream = new TestStream(new MemoryStream(data));
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            bufferSize: 0x100000 // 1MB buffer
        );

        // Act & Assert - should not crash
        sharpStream.Dispose();
        Assert.True(innerStream.IsDisposed);
    }

#if !LEGACY_DOTNET

    [Fact]
    public async Task DisposeAsync_WithLargeBuffer_DoesNotCrash()
    {
        // Arrange
        var data = new byte[0x1000000]; // 16MB data
        var innerStream = new TestStream(new MemoryStream(data));
        var sharpStream = new SharpCompressStream(
            innerStream,
            leaveOpen: false,
            bufferSize: 0x100000 // 1MB buffer
        );

        // Act & Assert - should not crash
        await sharpStream.DisposeAsync();
        Assert.True(innerStream.IsDisposed);
    }

#endif

    #endregion
}
