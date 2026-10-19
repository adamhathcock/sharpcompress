using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Ace.Headers;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Ace;

public class AceHeaderReadTests
{
    [Fact]
    public async Task Ace_Signature_Allows_Partial_Reads()
    {
        byte[] header = [0, 0, 0, 0, 0, 0, 0, 42, 42, 65, 67, 69, 42, 42];
        using var sync = new ShortReadStream(new MemoryStream(header));
        using var asyncInput = new ShortReadStream(new MemoryStream(header));
        Assert.True(AceHeader.IsArchive(sync));
        Assert.True(await AceHeader.IsArchiveAsync(asyncInput, CancellationToken.None));
    }
}
