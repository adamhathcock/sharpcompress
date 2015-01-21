SharpCompress
=============

Github mirror of http://sharpcompress.codeplex.com

SharpCompress is a compression library for .NET/Mono/Silverlight/WP7 that can unrar, un7zip, unzip, untar unbzip2 and ungzip with forward-only reading and file random access APIs. Write support for zip/tar/bzip2/gzip are implemented.

The major feature is support for non-seekable streams so large files can be processed on the fly (i.e. download stream). 

A Simple Request

Hi everyone. I hope you're using SharpCompress and finding it useful. Please give me feedback on what you'd like to see changed especially as far as usability goes. New feature suggestions are always welcome as well. I would also like to know what projects SharpCompress is being used in. I like seeing how it is used to give me ideas for future versions. Thanks!

Want to contribute?

I'm always looking for help or ideas. Please submit code or email with ideas. Unfortunately, just letting me know you'd like to help is not enough because I really have no overall plan of what needs to be done. I'll definitely accept code submissions and add you as a member of the project!

TODOs (always lots):
* RAR 5 support
* 7Zip writing
* Zip64
* Multi-volume Zip support.

Version 0.10.3:
==============
- Finally fixed Disposal issue when creating a new archive with the Archive API

Version 0.10.2:
==============
- Fixed Rar Header reading for invalid extended time headers.
- Windows Store assembly is now strong named
- Known issues with Long Tar names being worked on
- Updated to VS2013
	- Portable targets SL5 and Windows Phone 8 (up from SL4 and WP7)

Version 0.10.1:
==============
- Fixed 7Zip extraction performance problem

Version 0.10:
==============
- Added support for RAR Decryption (thanks to https://github.com/hrasyid)
- Embedded some BouncyCastle crypto classes to allow RAR Decryption and Winzip AES Decryption in Portable and Windows Store DLLs
- Built in Release (I think)

Some Help/Discussion:
https://sharpcompress.codeplex.com/discussions

7Zip implementation based on: https://code.google.com/p/managed-lzma/

LICENSE
Copyright (c) 2000 - 2011 The Legion Of The Bouncy Castle (http://www.bouncycastle.org)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
