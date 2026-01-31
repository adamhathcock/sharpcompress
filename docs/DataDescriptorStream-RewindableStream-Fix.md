# DataDescriptorStream and RewindableStream Fix

## Summary

Fixed the `Zip_Uncompressed_Read_All` test failure caused by incompatibility between `DataDescriptorStream` seeking requirements and the new `RewindableStream` wrapper used in `StreamingZipHeaderFactory`.

## Problem Description

### Symptom
The test `Zip_Uncompressed_Read_All` was failing with:
```
System.NotSupportedException : Cannot seek outside buffered region.
```

### Root Cause

The issue had two related aspects:

#### 1. Double-Wrapping of RewindableStream

`StreamingZipHeaderFactory.ReadStreamHeader()` was creating a new `RewindableStream` wrapper:
```csharp
var rewindableStream = new RewindableStream(stream);
```

When `ReaderFactory.OpenReader()` already wraps the input stream with `SeekableRewindableStream` (for seekable streams), this resulted in double-wrapping:
```
DataDescriptorStream
  -> NonDisposingStream
    -> RewindableStream (new, plain)  <-- created by ReadStreamHeader
      -> SeekableRewindableStream     <-- created by ReaderFactory
        -> FileStream
```

The inner plain `RewindableStream` lost the seeking capability of `SeekableRewindableStream`.

#### 2. Recording State Interference

Even after fixing the double-wrapping using `RewindableStream.EnsureSeekable()`, there was another issue:

`StreamingZipHeaderFactory.ReadStreamHeader()` contains code to peek ahead when checking for zero-length files with `UsePostDataDescriptor`:

```csharp
rewindableStream.StartRecording();
var nextHeaderBytes = reader.ReadUInt32();
rewindableStream.Rewind(true);
```

This code was interfering with the recording state that `ReaderFactory.OpenReader()` had set up:

1. `ReaderFactory.OpenReader()` calls `bStream.StartRecording()` at position 0
2. Factory detection calls `StreamingZipHeaderFactory.ReadStreamHeader()` via `IsZipFile()`
3. Inside `ReadStreamHeader`, the above code overwrites the recorded position
4. `Rewind(true)` stops recording and seeks to the wrong position
5. When control returns to `Factory.TryOpenReader()`, it calls `stream.Rewind(true)`, but recording is already stopped, so nothing happens
6. The stream position is not at the beginning, causing subsequent reads to fail

## Solution

### Fix 1: Use EnsureSeekable instead of new RewindableStream

Changed `StreamingZipHeaderFactory.ReadStreamHeader()` to use:
```csharp
var rewindableStream = RewindableStream.EnsureSeekable(stream);
```

This method:
- Returns the existing `RewindableStream` if the stream is already one (avoids double-wrapping)
- Creates a `SeekableRewindableStream` if the underlying stream is seekable
- Creates a plain `RewindableStream` only for non-seekable streams

### Fix 2: Use direct position save/restore for SeekableRewindableStream

For the peek-ahead logic, changed the code to check for `SeekableRewindableStream` specifically and use direct position manipulation:

```csharp
if (rewindableStream is SeekableRewindableStream)
{
    // Direct position save/restore avoids interfering with caller's recording state
    var savedPosition = rewindableStream.Position;
    var nextHeaderBytes = reader.ReadUInt32();
    rewindableStream.Position = savedPosition;
    header.HasData = !IsHeader(nextHeaderBytes);
}
else
{
    // Plain RewindableStream was created fresh by EnsureSeekable, safe to use recording
    rewindableStream.StartRecording();
    var nextHeaderBytes = reader.ReadUInt32();
    rewindableStream.Rewind(true);
    header.HasData = !IsHeader(nextHeaderBytes);
}
```

This approach:
- For `SeekableRewindableStream` (reused from caller): Uses direct position save/restore to avoid clobbering the caller's recording state
- For plain `RewindableStream` (freshly created): Uses the recording mechanism which is safe since the stream isn't shared

## Files Changed

- `src/SharpCompress/Common/Zip/StreamingZipHeaderFactory.cs`
- `src/SharpCompress/Common/Zip/StreamingZipHeaderFactory.Async.cs`

## Design Notes

### Why not fix RewindableStream.CanSeek?

`RewindableStream.CanSeek` returns `true` even though it can only seek within its buffered region. We considered changing this to `false`, but:
1. It would be a breaking change for existing code that relies on `CanSeek`
2. The `RewindableStream` does provide limited seeking capability (within buffer)
3. Checking for `SeekableRewindableStream` specifically is more precise

### Stream Wrapper Hierarchy

Understanding the stream wrapper hierarchy is crucial:

**For seekable source streams (e.g., FileStream):**
```
SeekableRewindableStream (full seeking via underlying stream)
  -> FileStream
```

**For non-seekable source streams (e.g., decompression streams):**
```
RewindableStream (limited seeking via buffer)
  -> DecompressionStream
```

`DataDescriptorStream` needs backward seeking to position the stream correctly after finding the data descriptor marker. This is why proper stream wrapper selection matters.
