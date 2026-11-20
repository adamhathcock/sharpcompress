# SharpCompress command-line tool

The `SharpCompress.Tool` .NET tool targets .NET 10 and provides four read-only commands:

```sh
sharpcompress formats [name]
sharpcompress detect backup.zip
sharpcompress inspect backup.tar.xz --json
sharpcompress list backup.rar --long
sharpcompress list backup.zip --ndjson
```

For development, run `dotnet run --project src/SharpCompress.Cli -- COMMAND ...`.
Build a tool package using `dotnet pack src/SharpCompress.Cli -c Release -o artifacts`.
Install it with `dotnet tool install --add-source ./artifacts SharpCompress.Tool --version 0.1.0`.
Run local-source installation from outside this repository if its NuGet source mapping
rejects `--add-source`, or supply a dedicated NuGet configuration for the local feed.

`--json` emits a versioned result document; `--ndjson` emits individual versioned records.
Unavailable fields are omitted from both formats and from detailed text output.
Known values such as `0` and `false` are still printed.
Listing preserves order and duplicate names with zero-based indexes. JSON output buffers
the listing; NDJSON streams records, and a later failure can leave a partial listing.
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

Detection recognizes supported archives, compressed TAR, and self-identifying standalone
BZip2, XZ, LZip and ZStandard streams in addition to GZip and Unix compress.
A standalone stream without an archive API is listed as one logical payload; its
display name is inferred, not stored metadata. Inspection reports its physical size
and detection result, not decoded sizes or format-specific block metadata.
Ordinary files report `unrecognized`, not an empty archive. Raw signatureless codecs
are not guessed. Extensionless inputs use content detection.

Deferred ZIP sizes, GZip placeholders, and shared compressed sizes are not
presented as known measurements. Checksums are
not included until their algorithm and availability can be exposed reliably.
Sequential listings/inspection can scan or decompress the whole input. None of these
commands claims payload integrity verification or recursively browses nested archives.
The formats catalog separates archive API support from standalone codec support;
it does not claim that every codec can be used inside every container.
