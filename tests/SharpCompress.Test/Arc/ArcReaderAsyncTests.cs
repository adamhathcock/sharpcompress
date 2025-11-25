using System.Threading.Tasks;
using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Arc;

public class ArcReaderAsyncTests : ReaderTests
{
    public ArcReaderAsyncTests()
    {
        UseExtensionInsteadOfNameToVerify = true;
        UseCaseInsensitiveToVerify = true;
    }

    [Fact]
    public async Task Arc_Uncompressed_Read_Async() =>
        await ReadAsync("Arc.uncompressed.arc", CompressionType.None);

    [Fact]
    public async Task Arc_Squeezed_Read_Async() =>
        await ReadAsync("Arc.squeezed.arc");

    [Fact]
    public async Task Arc_Crunched_Read_Async() =>
        await ReadAsync("Arc.crunched.arc");
}
