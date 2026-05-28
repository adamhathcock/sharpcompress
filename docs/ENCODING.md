# SharpCompress Character Encoding Guide

This guide explains how SharpCompress handles character encoding for archive entries (filenames, comments, etc.).

## Overview

Most archive formats store filenames and metadata as bytes. SharpCompress must convert these bytes to strings using the appropriate character encoding.

**Common Problem:** Archives created on systems with non-UTF8 encodings (especially Japanese, Chinese systems) appear with corrupted filenames when extracted on systems that assume UTF8.

---

## ArchiveEncoding Class

### Basic Usage

```csharp
using SharpCompress.Common;
using SharpCompress.Readers;

// Configure encoding using fluent factory method (preferred)
var options = ReaderOptions.ForEncoding(
    new ArchiveEncoding { Default = Encoding.GetEncoding(932) });  // cp932 for Japanese

using (var archive = ZipArchive.OpenArchive("japanese.zip", options))
{
    foreach (var entry in archive.Entries)
    {
        Console.WriteLine(entry.Key);  // Now shows correct characters
    }
}

// Alternative: object initializer
var options2 = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.GetEncoding(932) }
};
```

### ArchiveEncoding Properties

| Property | Purpose |
|----------|---------|
| `Default` | Default encoding for filenames (fallback) |
| `CustomDecoder` | Custom decoding function for special cases |

### Setting for Different APIs

**Archive API:**
```csharp
var options = ReaderOptions.ForEncoding(
    new ArchiveEncoding { Default = Encoding.GetEncoding(932) });
using (var archive = ZipArchive.OpenArchive("file.zip", options))
{
    // Use archive with correct encoding
}
```

**Reader API:**
```csharp
var options = ReaderOptions.ForEncoding(
    new ArchiveEncoding { Default = Encoding.GetEncoding(932) });
using (var stream = File.OpenRead("file.zip"))
using (var reader = ReaderFactory.OpenReader(stream, options))
{
    while (reader.MoveToNextEntry())
    {
        // Filenames decoded correctly
    }
}
```

---

## Common Encodings

### Asian Encodings

#### cp932 (Japanese)
```csharp
// Windows-31J, Shift-JIS variant used on Japanese Windows
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding(932)
    }
};
using (var archive = ZipArchive.OpenArchive("japanese.zip", options))
{
    // Correctly decodes Japanese filenames
}
```

**When to use:**
- Archives from Japanese Windows systems
- Files with Japanese characters in names

#### gb2312 (Simplified Chinese)
```csharp
// Simplified Chinese
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("gb2312")
    }
};
```

#### gbk (Extended Simplified Chinese)
```csharp
// Extended Simplified Chinese (more characters than gb2312)
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("gbk")
    }
};
```

#### big5 (Traditional Chinese)
```csharp
// Traditional Chinese (Taiwan, Hong Kong)
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("big5")
    }
};
```

#### euc-jp (Japanese, Unix)
```csharp
// Extended Unix Code for Japanese
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("eucjp")
    }
};
```

#### euc-kr (Korean)
```csharp
// Extended Unix Code for Korean
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("euc-kr")
    }
};
```

### Western European Encodings

#### iso-8859-1 (Latin-1)
```csharp
// Western European (includes accented characters)
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("iso-8859-1")
    }
};
```

**When to use:**
- Archives from French, German, Spanish systems
- Files with accented characters (é, ñ, ü, etc.)

#### cp1252 (Windows-1252)
```csharp
// Windows Western European
// Very similar to iso-8859-1 but with additional printable characters
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("cp1252")
    }
};
```

**When to use:**
- Archives from older Western European Windows systems
- Files with smart quotes and other Windows-specific characters

#### iso-8859-15 (Latin-9)
```csharp
// Western European with Euro symbol support
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("iso-8859-15")
    }
};
```

### Cyrillic Encodings

#### cp1251 (Windows Cyrillic)
```csharp
// Russian, Serbian, Bulgarian, etc.
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("cp1251")
    }
};
```

#### koi8-r (KOI8 Russian)
```csharp
// Russian (Unix standard)
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("koi8-r")
    }
};
```

### UTF Encodings (Modern)

#### UTF-8 (Default)
```csharp
// Modern standard - usually correct for new archives
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.UTF8
    }
};
```

#### UTF-16
```csharp
// Unicode - rarely used in archives
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.Unicode
    }
};
```

---

## Encoding Auto-Detection

SharpCompress attempts to auto-detect encoding, but this isn't always reliable:

```csharp
// Auto-detection (default)
using (var archive = ZipArchive.OpenArchive("file.zip"))  // Uses UTF8 by default
{
    // May show corrupted characters if archive uses different encoding
}

// Explicit encoding (more reliable)
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding { Default = Encoding.GetEncoding(932) }
};
using (var archive = ZipArchive.OpenArchive("file.zip", options))
{
    // Correct characters displayed
}
```

### When Manual Override is Needed

| Situation | Solution |
|-----------|----------|
| Archive shows corrupted characters | Specify the encoding explicitly |
| Archives from specific region | Use that region's encoding |
| Mixed encodings in archive | Use CustomDecoder |
| Testing with international files | Try different encodings |

---

## Custom Decoder

For complex scenarios where a single encoding isn't sufficient:

### Basic Custom Decoder

```csharp
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding
    {
        CustomDecoder = (data, offset, length) =>
        {
            // Custom decoding logic
            var bytes = new byte[length];
            Array.Copy(data, offset, bytes, 0, length);
            
            // Try UTF8 first
            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // Fallback to cp932 if UTF8 fails
                return Encoding.GetEncoding(932).GetString(bytes);
            }
        }
    }
};

using (var archive = ZipArchive.OpenArchive("mixed.zip", options))
{
    foreach (var entry in archive.Entries)
    {
        Console.WriteLine(entry.Key);  // Uses custom decoder
    }
}
```

### Advanced: Detect Encoding by Content

```csharp
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding
    {
        CustomDecoder = DetectAndDecode
    }
};

private static string DetectAndDecode(byte[] data, int offset, int length)
{
    var bytes = new byte[length];
    Array.Copy(data, offset, bytes, 0, length);
    
    // Try UTF8 (most modern archives)
    try
    {
        var str = Encoding.UTF8.GetString(bytes);
        // Verify it decoded correctly (no replacement characters)
        if (!str.Contains('\uFFFD'))
            return str;
    }
    catch { }
    
    // Try cp932 (Japanese)
    try
    {
        var str = Encoding.GetEncoding(932).GetString(bytes);
        if (!str.Contains('\uFFFD'))
            return str;
    }
    catch { }
    
    // Fallback to iso-8859-1 (always succeeds)
    return Encoding.GetEncoding("iso-8859-1").GetString(bytes);
}
```

---

## Code Examples

### Extract Archive with Japanese Filenames

```csharp
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding(932)  // cp932
    }
};

using (var archive = ZipArchive.OpenArchive("japanese_files.zip", options))
{
    archive.WriteToDirectory(@"C:\output");
}
// Files extracted with correct Japanese names
```

### Extract Archive with Western European Filenames

```csharp
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("iso-8859-1")
    }
};

using (var archive = ZipArchive.OpenArchive("french_files.zip", options))
{
    archive.WriteToDirectory(@"C:\output");
}
// Accented characters (é, è, ê, etc.) display correctly
```

### Extract Archive with Chinese Filenames

```csharp
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("gbk")  // Simplified Chinese
    }
};

using (var archive = ZipArchive.OpenArchive("chinese_files.zip", options))
{
    archive.WriteToDirectory(@"C:\output");
}
```

### Extract Archive with Russian Filenames

```csharp
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("cp1251")  // Windows Cyrillic
    }
};

using (var archive = ZipArchive.OpenArchive("russian_files.zip", options))
{
    archive.WriteToDirectory(@"C:\output");
}
```

### Reader API with Encoding

```csharp
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding(932)
    }
};

using (var stream = File.OpenRead("japanese.zip"))
using (var reader = ReaderFactory.OpenReader(stream, options))
{
    while (reader.MoveToNextEntry())
    {
        if (!reader.Entry.IsDirectory)
        {
            Console.WriteLine(reader.Entry.Key);  // Correct characters
            reader.WriteEntryToDirectory(@"C:\output");
        }
    }
}
```

---

## Creating Archives with Correct Encoding

When creating archives, SharpCompress uses UTF8 by default (recommended):

```csharp
// Create with UTF8 (default, recommended)
using (var archive = ZipArchive.CreateArchive())
{
    archive.AddAllFromDirectory(@"D:\my_files");
    archive.SaveTo("output.zip", CompressionType.Deflate);
    // Archives created with UTF8 encoding
}
```

If you need to create archives for systems that expect specific encodings:

```csharp
// Note: SharpCompress Writer API uses UTF8 for encoding
// To create archives with other encodings, consider:
// 1. Let users on those systems create archives
// 2. Use system tools (7-Zip, WinRAR) with desired encoding
// 3. Post-process archives if absolutely necessary

// For now, recommend modern UTF8-based archives
```

---

## Troubleshooting Encoding Issues

### Filenames Show Question Marks (?)

```
✗ Wrong encoding detected
test文件.txt → test???.txt
```

**Solution:** Specify correct encoding explicitly

```csharp
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding 
    { 
        Default = Encoding.GetEncoding("gbk")  // Try different encodings
    }
};
```

### Filenames Show Replacement Character (￿)

```
✗ Invalid bytes for selected encoding
café.txt → caf￿.txt
```

**Solution:**
1. Try a different encoding (see Common Encodings table)
2. Use CustomDecoder with fallback encoding
3. Archive might be corrupted

### Mixed Encodings in Single Archive

```csharp
// Use CustomDecoder to handle mixed encodings
var options = new ReaderOptions
{
    ArchiveEncoding = new ArchiveEncoding
    {
        CustomDecoder = (data, offset, length) =>
        {
            // Try multiple encodings in priority order
            var bytes = new byte[length];
            Array.Copy(data, offset, bytes, 0, length);
            
            foreach (var encoding in new[] 
            {
                Encoding.UTF8,
                Encoding.GetEncoding(932),
                Encoding.GetEncoding("iso-8859-1")
            })
            {
                try
                {
                    var str = encoding.GetString(bytes);
                    if (!str.Contains('\uFFFD'))
                        return str;
                }
                catch { }
            }
            
            // Final fallback
            return Encoding.GetEncoding("iso-8859-1").GetString(bytes);
        }
    }
};
```

---

## Encoding Reference Table

| Encoding | Code | Use Case |
|----------|------|----------|
| UTF-8 | (default) | Modern archives, recommended |
| cp932 | 932 | Japanese Windows |
| gb2312 | "gb2312" | Simplified Chinese |
| gbk | "gbk" | Extended Simplified Chinese |
| big5 | "big5" | Traditional Chinese |
| iso-8859-1 | "iso-8859-1" | Western European |
| cp1252 | "cp1252" | Windows Western European |
| cp1251 | "cp1251" | Russian/Cyrillic |
| euc-jp | "euc-jp" | Japanese Unix |
| euc-kr | "euc-kr" | Korean |

---

## Best Practices

1. **Use UTF-8 for new archives** - Most modern systems support it
2. **Ask the archive creator** - When receiving archives with corrupted names
3. **Provide encoding options** - If your app handles user archives
4. **Document your assumption** - Tell users what encoding you're using
5. **Test with international files** - Before releasing production code

---

## Related Documentation

- [USAGE.md](USAGE.md#extract-zip-which-has-non-utf8-encoded-filenamycp932) - Usage examples
