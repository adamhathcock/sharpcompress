using CommandLine;

namespace SharpCompress
{
    public class Program
    {
        public static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<InfoOptions, ExtractOptions>(args)
                              .MapResult(
                                         (InfoOptions opts) => opts.Process(),
                                         (ExtractOptions opts) => opts.Process(),
                                         errs => 1);
        }
    }
}