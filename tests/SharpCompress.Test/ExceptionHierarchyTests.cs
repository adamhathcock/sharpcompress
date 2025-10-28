using System;
using SharpCompress.Common;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Xz;
using Xunit;

namespace SharpCompress.Test;

public class ExceptionHierarchyTests
{
    [Fact]
    public void AllSharpCompressExceptions_InheritFromSharpCompressException()
    {
        // Verify that ArchiveException inherits from SharpCompressException
        Assert.True(typeof(SharpCompressException).IsAssignableFrom(typeof(ArchiveException)));

        // Verify that ExtractionException inherits from SharpCompressException
        Assert.True(typeof(SharpCompressException).IsAssignableFrom(typeof(ExtractionException)));

        // Verify that InvalidFormatException inherits from SharpCompressException (through ExtractionException)
        Assert.True(
            typeof(SharpCompressException).IsAssignableFrom(typeof(InvalidFormatException))
        );

        // Verify that CryptographicException inherits from SharpCompressException
        Assert.True(
            typeof(SharpCompressException).IsAssignableFrom(typeof(CryptographicException))
        );

        // Verify that IncompleteArchiveException inherits from SharpCompressException (through ArchiveException)
        Assert.True(
            typeof(SharpCompressException).IsAssignableFrom(typeof(IncompleteArchiveException))
        );

        // Verify that ReaderCancelledException inherits from SharpCompressException
        Assert.True(
            typeof(SharpCompressException).IsAssignableFrom(typeof(ReaderCancelledException))
        );

        // Verify that MultipartStreamRequiredException inherits from SharpCompressException (through ExtractionException)
        Assert.True(
            typeof(SharpCompressException).IsAssignableFrom(
                typeof(MultipartStreamRequiredException)
            )
        );

        // Verify that MultiVolumeExtractionException inherits from SharpCompressException (through ExtractionException)
        Assert.True(
            typeof(SharpCompressException).IsAssignableFrom(typeof(MultiVolumeExtractionException))
        );

        // Verify that ZlibException inherits from SharpCompressException
        Assert.True(typeof(SharpCompressException).IsAssignableFrom(typeof(ZlibException)));

        // Verify that XZIndexMarkerReachedException inherits from SharpCompressException
        Assert.True(
            typeof(SharpCompressException).IsAssignableFrom(typeof(XZIndexMarkerReachedException))
        );
    }

    [Fact]
    public void SharpCompressException_CanBeCaughtByBaseType()
    {
        // Test that a derived exception can be caught as SharpCompressException
        var exception = new InvalidFormatException("Test message");
        var caughtException = false;

        try
        {
            throw exception;
        }
        catch (SharpCompressException ex)
        {
            caughtException = true;
            Assert.Same(exception, ex);
        }

        Assert.True(caughtException, "Exception should have been caught as SharpCompressException");
    }

    [Fact]
    public void InternalLzmaExceptions_InheritFromSharpCompressException()
    {
        // Use reflection to verify internal exception types
        var dataErrorExceptionType = Type.GetType(
            "SharpCompress.Compressors.LZMA.DataErrorException, SharpCompress"
        );
        Assert.NotNull(dataErrorExceptionType);
        Assert.True(typeof(SharpCompressException).IsAssignableFrom(dataErrorExceptionType));

        var invalidParamExceptionType = Type.GetType(
            "SharpCompress.Compressors.LZMA.InvalidParamException, SharpCompress"
        );
        Assert.NotNull(invalidParamExceptionType);
        Assert.True(typeof(SharpCompressException).IsAssignableFrom(invalidParamExceptionType));
    }

    [Fact]
    public void ExceptionConstructors_WorkCorrectly()
    {
        // Test parameterless constructor
        var ex1 = new SharpCompressException();
        Assert.NotNull(ex1);

        // Test message constructor
        var ex2 = new SharpCompressException("Test message");
        Assert.Equal("Test message", ex2.Message);

        // Test message and inner exception constructor
        var inner = new InvalidOperationException("Inner");
        var ex3 = new SharpCompressException("Test message", inner);
        Assert.Equal("Test message", ex3.Message);
        Assert.Same(inner, ex3.InnerException);
    }
}
