# Release compatibility notes

## 0.50.9

0.50.9 introduced process-wide [decompression dictionary limits](API.md#decompression-dictionary-limits).
The new 64 MiB LZMA2 limit can make valid 7z and XZ archives that extracted in
0.50.8 throw `InvalidFormatException`. For trusted archives that need a larger
dictionary, configure the limit once at application startup, before opening
archives:

```csharp
SharpCompress.Common.DictionarySizeLimits.Lzma2 = 256L << 20; // 256 MiB
```

This also raises the limit for other LZMA2 streams in the same process. Keep
extraction output and concurrency limits separate.
