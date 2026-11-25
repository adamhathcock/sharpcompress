# SharpCompress command-line tool

The SharpCompress CLI supports Native AOT on Linux x64, macOS arm64, and
Windows x64. Building uses the .NET 11 SDK; the native executable does not
require a separate .NET runtime. It provides four read-only commands:

```sh
sharpcompress formats [name]
sharpcompress detect backup.zip
sharpcompress inspect backup.tar.xz --json
sharpcompress list backup.rar --long
sharpcompress list backup.zip --ndjson
```

For development, run `dotnet run --project src/SharpCompress.Cli -- COMMAND ...`.
To build and run the native executable, use a matching host with the
[.NET Native AOT prerequisites][aot] installed (macOS arm64 shown here):

```sh
dotnet publish src/SharpCompress.Cli -c Release -r osx-arm64 \
  -p:NuGetLockFilePath=obj/packages.aot.lock.json -o ./cli-native
./cli-native/SharpCompress.Cli formats --json
```

The **Native CLI packages** workflow in GitHub Actions builds, installs, and
tests the CLI on all three platforms. To build the tool packages locally, pass
the host RID as a restore property so `dotnet pack` can build both the tool
manifest package and the host's native package:

```sh
dotnet pack src/SharpCompress.Cli -c Release -o ./cli-packages \
  -p:RuntimeIdentifiers=osx-arm64 -p:NuGetLockFilePath=obj/packages.aot.lock.json
```

[aot]: https://learn.microsoft.com/dotnet/core/deploying/native-aot/#prerequisites

`--json` emits a versioned result document; `--ndjson` emits individual versioned records.
Unavailable fields are omitted from both formats and from detailed text output.
Known values such as `0` and `false` are still printed.
Listing preserves order and duplicate names with zero-based indexes. JSON output buffers
the listing; text and NDJSON stream it, so a later failure (such as a missing archive
volume needed to complete an entry) can leave a partial listing.
Errors go to stderr. Exit codes: 0 success, 1 unrecognized input/no catalog match,
2 invalid arguments, 3 file access failure, 4 unsupported/corrupt/encrypted input,
130 cancellation. Partial inspection due to unavailable metadata is not an error.

Options: `--encoding NAME`, `--look-for-header` for self-extracting RAR,
and `--password-env VARIABLE` for encrypted archives. Passwords are not printed.
`--long` applies only to `list`. Inputs are file paths, not stdin or directory trees.

Detailed TAR listings (`list --long`, `--json`, or `--ndjson`) include a `tar`
object alongside each entry's `metadata`. `mode` is an octal string (for example,
`"0640"`) and may include file-type bits if stored in the archive. `userId` and
`groupId` are numbers. Directory modes are currently omitted because the
library's public mode adds a marker that overlaps the sticky bit. Plain `list`
still prints only names.
Control characters and Unicode line separators in plain names are escaped as `\uXXXX`;
JSON and NDJSON retain the original key values.

A detailed ZIP listing includes `zip.comment` for entries with a non-empty
comment. The ZIP archive comment is separate and appears as `information.comment`
in `inspect`.

Detection recognizes supported archives, compressed TAR, and self-identifying standalone
BZip2, XZ, LZip and ZStandard streams in addition to GZip and Unix compress.
A standalone stream without an archive API is listed as one logical payload; its
display name is inferred, not stored metadata. Inspection reports its physical size
and detection result, not decoded sizes or format-specific block metadata.
Ordinary files report `unrecognized`, not an empty archive. Raw signatureless codecs
are not guessed. Extensionless inputs use content detection.

Deferred ZIP sizes, standalone GZip and Unix compress size placeholders, and shared
compressed sizes (including those of wrapped TAR entries) are omitted. Per-entry
7z volume indexes are also omitted because the volume mapping is unavailable. Checksums are
not included until their algorithm and availability can be exposed reliably.
Sequential listings/inspection can scan or decompress the whole input. None of these
commands claims payload integrity verification or recursively browses nested archives.
The formats catalog separates archive API support from standalone codec support;
it does not claim that every codec can be used inside every container.
