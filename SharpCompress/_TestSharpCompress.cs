using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SharpCompress.Archive;
using SharpCompress.Archive.Zip;
using SharpCompress.Common;

namespace SharpCompress
{
    static class _TestSharpCompress
    {
        public static void Main(string[] args) {
            string SCRATCH_FILES_PATH = "ziptest";
            string scratchPath = "ziptest.zip";

             using (var archive = ZipArchive.Create()) 
             { 
                 archive.AddAllFromDirectory(SCRATCH_FILES_PATH); 
                 archive.SaveTo(scratchPath, CompressionType.Deflate); 
             }
            //write my zipfile with head data
             using (FileStream fs = new FileStream("mypack.data.zip", FileMode.Create, FileAccess.ReadWrite, FileShare.Read)) {
                 MyHead mh = new MyHead();
                 byte[] headData = mh.Create();
                 fs.Write(headData, 0, headData.Length);
                 using (FileStream fs2 = new FileStream(scratchPath, FileMode.Open, FileAccess.Read)) {
                     byte[] buf = new byte[1024];
                     int rc=0;
                     while( (rc= fs2.Read(buf, 0, buf.Length)) >0){
                         fs.Write(buf, 0, rc);
                     }
                 }
             }
            //
            //read my zip file with head
            //
             using (FileStream fs = new FileStream("mypack.data.zip", FileMode.Open, FileAccess.Read, FileShare.Read)) {
                 byte[] buf = new byte[1024];
                 int offset=fs.Read(buf, 0, buf.Length);
                 System.Diagnostics.Debug.Assert(offset==1024);
                 ZipArchive zip=ZipArchive.Open(fs, Options.LookForHeader);//cann't read
                 //ZipArchive zip = ZipArchive.Open(fs, Options.None); //will throw exption
                 //ZipArchive zip = ZipArchive.Open(fs, Options.KeepStreamsOpen);//cann't read
                 
                 foreach (ZipArchiveEntry zf in zip.Entries) {
                     Console.WriteLine(zf.Key);
                     //bug:the will not none in zipfile
                 }
             }
        }
        public class MyHead {
            public int Id = 10086;
            public string Ver = "1.0.1";
            public string Flag = "MyHead";
            public byte[] Create() {
                byte[] buf = null;
                using (MemoryStream ms = new MemoryStream(1024)) {
                    ms.SetLength(1024);
                    using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8)) {
                        bw.Write(this.Flag);
                        bw.Write(this.Id);
                        bw.Write(this.Ver);
                        
                    }
                    
                    buf = ms.ToArray();
                }
                return buf;
            } 
        }
    }
}
