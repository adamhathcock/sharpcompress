using SharpCompress.Common;
using Xunit;

namespace SharpCompress.Test.Lzw;

public class LzwReaderAsyncTests : ReaderTests
{
    public LzwReaderAsyncTests() => UseExtensionInsteadOfNameToVerify = true;

    [Fact]
    public async System.Threading.Tasks.Task Lzw_Reader_Async()
    {
        await ReadAsync("Tar.tar.Z", CompressionType.Lzw);
    }
}
