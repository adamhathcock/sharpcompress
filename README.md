# SharpCompress

SharpCompress is a compression library for .NET/Mono/Silverlight/WP7 that can unrar, un7zip, unzip, untar unbzip2 and ungzip with forward-only reading and file random access APIs. Write support for zip/tar/bzip2/gzip are implemented.

The major feature is support for non-seekable streams so large files can be processed on the fly (i.e. download stream). 

[![Build status](https://ci.appveyor.com/api/projects/status/voxg971oemmvxh1e/branch/master?svg=true)](https://ci.appveyor.com/project/adamhathcock/sharpcompress/branch/master)

## Need Help?
Post Issues on Github!

Check the [Supported Formats](FORMATS.md) and [Basic Usage.](USAGE.md)

## A Simple Request

Hi everyone. I hope you're using SharpCompress and finding it useful. Please give me feedback on what you'd like to see changed especially as far as usability goes. New feature suggestions are always welcome as well. I would also like to know what projects SharpCompress is being used in. I like seeing how it is used to give me ideas for future versions. Thanks!

Please do not email me directly to ask for help.  If you think there is a real issue, please report it here.

## Want to contribute?

I'm always looking for help or ideas. Please submit code or email with ideas. Unfortunately, just letting me know you'd like to help is not enough because I really have no overall plan of what needs to be done. I'll definitely accept code submissions and add you as a member of the project!

## TODOs (always lots)

* RAR 5 support
* 7Zip writing
* Zip64
* Multi-volume Zip support.
* RAR5 support

## Version Log

### Version 0.14.1

* [.NET Assemblies aren't strong named](https://github.com/adamhathcock/sharpcompress/issues/158)
* [Pkware encryption for Zip files didn't allow for multiple reads of an entry](https://github.com/adamhathcock/sharpcompress/issues/197)
* [GZip Entry couldn't be read multiple times](https://github.com/adamhathcock/sharpcompress/issues/198)

### Version 0.14.0

* [Support for LZip reading in for Tars](https://github.com/adamhathcock/sharpcompress/pull/191)

### Version 0.13.1

* [Fix null password on ReaderFactory. Fix null options on SevenZipArchive](https://github.com/adamhathcock/sharpcompress/pull/188)
* [Make PpmdProperties lazy to avoid unnecessary allocations.](https://github.com/adamhathcock/sharpcompress/pull/185)

### Version 0.13.0

* Breaking change: Big refactor of Options on API.
* 7Zip supports Deflate

### Version 0.12.4

* Forward only zip issue fix https://github.com/adamhathcock/sharpcompress/issues/160
* Try to fix frameworks again by copying targets from JSON.NET

### Version 0.12.3

* 7Zip fixes https://github.com/adamhathcock/sharpcompress/issues/73
* Maybe all profiles will work with project.json now

### Version 0.12.2

* Support Profile 259 again

### Version 0.12.1

* Support Silverlight 5

### Version 0.12.0

* .NET Core RTM!
* Bug fix for Tar long paths

### Version 0.11.6

* Bug fix for global header in Tar
* Writers now have a leaveOpen `bool` overload.  They won't close streams if not-requested to.

### Version 0.11.5

* Bug fix in Skip method

### Version 0.11.4

* SharpCompress is now endian neutral (matters for Mono platforms)
* Fix for Inflate (need to change implementation)
* Fixes for RAR detection

### Version 0.11.1

* Added Cancel on IReader
* Removed .NET 2.0 support and LinqBridge dependency

### Version 0.11

* Been over a year, contains mainly fixes from contributors!  
* Possible breaking change: ArchiveEncoding is UTF8 by default now.
* TAR supports writing long names using longlink
* RAR Protect Header added

### Version 0.10.3

* Finally fixed Disposal issue when creating a new archive with the Archive API

### Version 0.10.2

* Fixed Rar Header reading for invalid extended time headers.
* Windows Store assembly is now strong named
* Known issues with Long Tar names being worked on
* Updated to VS2013
* Portable targets SL5 and Windows Phone 8 (up from SL4 and WP7)

### Version 0.10.1

* Fixed 7Zip extraction performance problem

### Version 0.10:

* Added support for RAR Decryption (thanks to https://github.com/hrasyid)
* Embedded some BouncyCastle crypto classes to allow RAR Decryption and Winzip AES Decryption in Portable and Windows Store DLLs
* Built in Release (I think)

Some Help/Discussion: https://sharpcompress.codeplex.com/discussions

7Zip implementation based on: https://code.google.com/p/managed-lzma/

LICENSE
Copyright (c) 2000 - 2011 The Legion Of The Bouncy Castle (http://www.bouncycastle.org)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
