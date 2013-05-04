using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace SharpCompress
{
    public sealed class ReaderFactory : IReaderFactory
    {
        /// <summary>
        /// Opens a Reader for Non-seeking usage
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="listener"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public IAsyncOperation<IReader> Open(IInputStream stream)
        {
            return AsyncInfo.Run(x => OpenPrivate(stream));
        }

        private Task<IReader> OpenPrivate(IInputStream stream)
        {
            return Task.Run<IReader>(() => new WrappedReader(SharpCompress.Reader.ReaderFactory.Open(stream.AsStreamForRead())));
        }
    }
}
