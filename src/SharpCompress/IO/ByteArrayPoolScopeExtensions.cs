using System.IO;

namespace SharpCompress.IO
{
    public static class ByteArrayPoolScopeExtensions
    {
        public static int Read(this Stream stream, ByteArrayPoolScope scope)
        {
            return stream.Read(scope.Array, scope.Offset, scope.Count);
        }

        public static ByteArrayPoolScope ReadScope(this BinaryReader stream, int count)
        {
            var scope = ByteArrayPool.RentScope(count);
            int numRead = 0;
            do
            {
                int n = stream.Read(scope.Array, numRead, count);
                if (n == 0)
                {
                    break;
                }
                numRead += n;
                count -= n;
            } while (count > 0);
            scope.OverrideSize(numRead);
            return scope;
        }
    }
}