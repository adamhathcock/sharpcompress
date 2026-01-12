# SharpCompress

SharpCompress is a compression library in pure C# for .NET Framework 4.8, .NET 8.0 and .NET 10.0 that can unrar, un7zip, unzip, untar unbzip2, ungzip, unlzip, unzstd, unarc and unarj with forward-only reading and file random access APIs. Write support for zip/tar/bzip2/gzip/lzip are implemented.

The major feature is support for non-seekable streams so large files can be processed on the fly (i.e. download stream).

**NEW:** All I/O operations now support async/await for improved performance and scalability. See the [USAGE.md](docs/USAGE.md#async-examples) for examples.

GitHub Actions Build -
[![SharpCompress](https://github.com/adamhathcock/sharpcompress/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/adamhathcock/sharpcompress/actions/workflows/dotnetcore.yml)
[![Static Badge](https://img.shields.io/badge/API%20Docs-DNDocs-190088?logo=readme&logoColor=white)](https://dndocs.com/d/sharpcompress/api/index.html)

## Need Help?

Post Issues on Github!

Check the [Supported Formats](docs/FORMATS.md) and [Basic Usage.](docs/USAGE.md)

## Recommended Formats

In general, I recommend GZip (Deflate)/BZip2 (BZip)/LZip (LZMA) as the simplicity of the formats lend to better long term archival as well as the streamability. Tar is often used in conjunction for multiple files in a single archive (e.g. `.tar.gz`)

Zip is okay, but it's a very hap-hazard format and the variation in headers and implementations makes it hard to get correct. Uses Deflate by default but supports a lot of compression methods.

RAR is not recommended as it's a proprietary format and the compression is closed source. Use Tar/LZip for LZMA

7Zip and XZ both are overly complicated. 7Zip does not support streamable formats. XZ has known holes explained here: (http://www.nongnu.org/lzip/xz_inadequate.html) Use Tar/LZip for LZMA compression instead.

ZStandard is an efficient format that works well for streaming with a flexible compression level to tweak the speed/performance trade off you are looking for.  We currently only implement decompression for ZStandard but as we leverage the [ZstdSharp](https://github.com/oleg-st/ZstdSharp) library one could likely add compression support without much trouble (PRs are welcome!).

## A Simple Request

Hi everyone. I hope you're using SharpCompress and finding it useful. Please give me feedback on what you'd like to see changed especially as far as usability goes. New feature suggestions are always welcome as well. I would also like to know what projects SharpCompress is being used in. I like seeing how it is used to give me ideas for future versions. Thanks!

Please do not email me directly to ask for help. If you think there is a real issue, please report it here.

## Want to contribute?

I'm always looking for help or ideas. Please submit code or email with ideas. Unfortunately, just letting me know you'd like to help is not enough because I really have no overall plan of what needs to be done. I'll definitely accept code submissions and add you as a member of the project!

## Notes

XZ implementation based on: https://github.com/sambott/XZ.NET by @sambott

XZ BCJ filters support contributed by Louis-Michel Bergeron, on behalf of aDolus Technology Inc. - 2022

7Zip implementation based on: https://code.google.com/p/managed-lzma/

LICENSE
Copyright (c) 2000 - 2011 The Legion Of The Bouncy Castle (http://www.bouncycastle.org)

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
