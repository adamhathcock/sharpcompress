using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;

namespace SharpCompress
{
    public class BaseOptions
    {
        [Value(0, Min = 1)]
        public IEnumerable<string> Path { get; set; }

        protected IEnumerable<FileInfo> GetFilesFromPath()
        {
            foreach (var s in Path)
            {
                var fileInfo = new FileInfo(s);
                if (fileInfo.Exists)
                {
                    yield return fileInfo;
                }
                else
                {
                    using (ConsoleHelper.PushError())
                    {
                        Console.WriteLine($"{s} does not exist");
                    }
                }
            }
        }
    }
}