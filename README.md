SharpCompress
=============

Github mirror of http://sharpcompress.codeplex.com

SharpCompress is a compression library for .NET/Mono/Silverlight/WP7 that can unrar, un7zip, unzip, untar unbzip2 and ungzip with forward-only reading and file random access APIs. Write support for zip/tar/bzip2/gzip is implemented.

The major feature is support for non-seekable streams so large files can be processed on the fly (i.e. download stream). 

A Simple Request

Hi everyone. I hope you're using SharpCompress and finding it useful. Please give me feedback on what you'd like to see changed especially as far as usability goes. New feature suggestions are always welcome as well. I would also like to know what projects SharpCompress is being used in. I like seeing how it is used to give me ideas for future versions. Thanks!

Want to contribute?

I'm always looking for help or ideas. Please submit code or email with ideas. Unfortunately, just letting me know you'd like to help is not enough because I really have no overall plan of what needs to be done. I'll definitely accept code submissions and add you as a member of the project!

TODOs (always lots):
* 7Zip writing
* RAR Decryption
* Zip64
* Multi-volume Zip support.

7Zip implementation based on: https://code.google.com/p/managed-lzma/