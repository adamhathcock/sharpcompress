# 7z Format Description (4.59)

Source: https://github.com/jljusten/LZMA-SDK/blob/master/DOC/7zFormat.txt

Raw download used for this conversion: https://raw.githubusercontent.com/jljusten/LZMA-SDK/master/DOC/7zFormat.txt

Downloaded and converted on 2026-05-23.

This is a Markdown conversion of the LZMA SDK plaintext 7z archive format description. Pseudo-grammar blocks preserve the source field names and spelling.

## Contents

- [Overview](#overview)
- [Format Structure Overview](#format-structure-overview)
- [Notes About Notation and Encoding](#notes-about-notation-and-encoding)
- [Property IDs](#property-ids)
- [7z Format Headers](#7z-format-headers)
  - [SignatureHeader](#signatureheader)
  - [ArchiveProperties](#archiveproperties)
  - [Digests](#digests-numstreams)
  - [PackInfo](#packinfo)
  - [Folder](#folder)
  - [Coders Info](#coders-info)
  - [SubStreams Info](#substreams-info)
  - [Streams Info](#streams-info)
  - [FilesInfo](#filesinfo)
  - [Header](#header)
  - [HeaderInfo](#headerinfo)

## Overview

This file contains a description of the 7z archive format.

A 7z archive can contain files compressed with any method. See `Methods.txt` in the LZMA SDK for descriptions of defined compression methods.

## Format Structure Overview

Some fields can be optional.

### Archive Structure

```text
SignatureHeader
[PackedStreams]
[PackedStreamsForHeaders]
[
  Header
  or
  {
    Packed Header
    HeaderInfo
  }
]
```

### Header Structure

```text
{
  ArchiveProperties
  AdditionalStreams
  {
    PackInfo
    {
      PackPos
      NumPackStreams
      Sizes[NumPackStreams]
      CRCs[NumPackStreams]
    }
    CodersInfo
    {
      NumFolders
      Folders[NumFolders]
      {
        NumCoders
        CodersInfo[NumCoders]
        {
          ID
          NumInStreams;
          NumOutStreams;
          PropertiesSize
          Properties[PropertiesSize]
        }
        NumBindPairs
        BindPairsInfo[NumBindPairs]
        {
          InIndex;
          OutIndex;
        }
        PackedIndices
      }
      UnPackSize[Folders][Folders.NumOutstreams]
      CRCs[NumFolders]
    }
    SubStreamsInfo
    {
      NumUnPackStreamsInFolders[NumFolders];
      UnPackSizes[]
      CRCs[]
    }
  }
  MainStreamsInfo
  {
    (Same as in AdditionalStreams)
  }
  FilesInfo
  {
    NumFiles
    Properties[]
    {
      ID
      Size
      Data
    }
  }
}
```

### HeaderInfo Structure

```text
{
  (Same as in AdditionalStreams)
}
```

## Notes About Notation and Encoding

7z uses little-endian encoding.

Optional headers are marked as:

```text
[]
Header
[]
```

`REAL_UINT64` means a real `UINT64`.

`UINT64` means a real `UINT64` encoded with the following scheme. The size of the encoding sequence depends on the first byte:

| First byte (binary) | Extra bytes | Value |
| --- | --- | --- |
| `0xxxxxxx` | none | `( xxxxxxx )` |
| `10xxxxxx` | `BYTE y[1]` | `(  xxxxxx << (8 * 1)) + y` |
| `110xxxxx` | `BYTE y[2]` | `(   xxxxx << (8 * 2)) + y` |
| `...` | `...` | `...` |
| `1111110x` | `BYTE y[6]` | `(       x << (8 * 6)) + y` |
| `11111110` | `BYTE y[7]` | `y` |
| `11111111` | `BYTE y[8]` | `y` |

## Property IDs

| ID | Name |
| --- | --- |
| `0x00` | `kEnd` |
| `0x01` | `kHeader` |
| `0x02` | `kArchiveProperties` |
| `0x03` | `kAdditionalStreamsInfo` |
| `0x04` | `kMainStreamsInfo` |
| `0x05` | `kFilesInfo` |
| `0x06` | `kPackInfo` |
| `0x07` | `kUnPackInfo` |
| `0x08` | `kSubStreamsInfo` |
| `0x09` | `kSize` |
| `0x0A` | `kCRC` |
| `0x0B` | `kFolder` |
| `0x0C` | `kCodersUnPackSize` |
| `0x0D` | `kNumUnPackStream` |
| `0x0E` | `kEmptyStream` |
| `0x0F` | `kEmptyFile` |
| `0x10` | `kAnti` |
| `0x11` | `kName` |
| `0x12` | `kCTime` |
| `0x13` | `kATime` |
| `0x14` | `kMTime` |
| `0x15` | `kWinAttributes` |
| `0x16` | `kComment` |
| `0x17` | `kEncodedHeader` |
| `0x18` | `kStartPos` |
| `0x19` | `kDummy` |

## 7z Format Headers

### SignatureHeader

```text
BYTE kSignature[6] = {'7', 'z', 0xBC, 0xAF, 0x27, 0x1C};

ArchiveVersion
{
  BYTE Major;   // now = 0
  BYTE Minor;   // now = 2
};

UINT32 StartHeaderCRC;

StartHeader
{
  REAL_UINT64 NextHeaderOffset
  REAL_UINT64 NextHeaderSize
  UINT32 NextHeaderCRC
}
```

### ArchiveProperties

```text
BYTE NID::kArchiveProperties (0x02)
for (;;)
{
  BYTE PropertyType;
  if (aType == 0)
    break;
  UINT64 PropertySize;
  BYTE PropertyData[PropertySize];
}
```

### Digests (NumStreams)

```text
BYTE AllAreDefined
if (AllAreDefined == 0)
{
  for(NumStreams)
    BIT Defined
}
UINT32 CRCs[NumDefined]
```

### PackInfo

```text
BYTE NID::kPackInfo  (0x06)
UINT64 PackPos
UINT64 NumPackStreams

[]
BYTE NID::kSize    (0x09)
UINT64 PackSizes[NumPackStreams]
[]

[]
BYTE NID::kCRC      (0x0A)
PackStreamDigests[NumPackStreams]
[]

BYTE NID::kEnd
```

### Folder

```text
UINT64 NumCoders;
for (NumCoders)
{
  BYTE
  {
    0:3 CodecIdSize
    4:  Is Complex Coder
    5:  There Are Attributes
    6:  Reserved
    7:  There are more alternative methods. (Not used anymore, must be 0).
  }
  BYTE CodecId[CodecIdSize]
  if (Is Complex Coder)
  {
    UINT64 NumInStreams;
    UINT64 NumOutStreams;
  }
  if (There Are Attributes)
  {
    UINT64 PropertiesSize
    BYTE Properties[PropertiesSize]
  }
}

NumBindPairs = NumOutStreamsTotal - 1;

for (NumBindPairs)
{
  UINT64 InIndex;
  UINT64 OutIndex;
}

NumPackedStreams = NumInStreamsTotal - NumBindPairs;
if (NumPackedStreams > 1)
  for(NumPackedStreams)
  {
    UINT64 Index;
  };
```

### Coders Info

```text
BYTE NID::kUnPackInfo  (0x07)

BYTE NID::kFolder  (0x0B)
UINT64 NumFolders
BYTE External
switch(External)
{
  case 0:
    Folders[NumFolders]
  case 1:
    UINT64 DataStreamIndex
}

BYTE ID::kCodersUnPackSize  (0x0C)
for(Folders)
  for(Folder.NumOutStreams)
   UINT64 UnPackSize;

[]
BYTE NID::kCRC   (0x0A)
UnPackDigests[NumFolders]
[]

BYTE NID::kEnd
```

### SubStreams Info

```text
BYTE NID::kSubStreamsInfo; (0x08)

[]
BYTE NID::kNumUnPackStream; (0x0D)
UINT64 NumUnPackStreamsInFolders[NumFolders];
[]

[]
BYTE NID::kSize  (0x09)
UINT64 UnPackSizes[]
[]

[]
BYTE NID::kCRC  (0x0A)
Digests[Number of streams with unknown CRC]
[]

BYTE NID::kEnd
```

### Streams Info

```text
[]
PackInfo
[]

[]
CodersInfo
[]

[]
SubStreamsInfo
[]

BYTE NID::kEnd
```

### FilesInfo

```text
BYTE NID::kFilesInfo;  (0x05)
UINT64 NumFiles

for (;;)
{
  BYTE PropertyType;
  if (aType == 0)
    break;

  UINT64 Size;

  switch(PropertyType)
  {
    kEmptyStream:   (0x0E)
      for(NumFiles)
        BIT IsEmptyStream

    kEmptyFile:     (0x0F)
      for(EmptyStreams)
        BIT IsEmptyFile

    kAnti:          (0x10)
      for(EmptyStreams)
        BIT IsAntiFile

    case kCTime: (0x12)
    case kATime: (0x13)
    case kMTime: (0x14)
      BYTE AllAreDefined
      if (AllAreDefined == 0)
      {
        for(NumFiles)
          BIT TimeDefined
      }
      BYTE External;
      if(External != 0)
        UINT64 DataIndex
      []
      for(Definded Items)
        UINT64 Time
      []

    kNames:     (0x11)
      BYTE External;
      if(External != 0)
        UINT64 DataIndex
      []
      for(Files)
      {
        wchar_t Names[NameSize];
        wchar_t 0;
      }
      []

    kAttributes:  (0x15)
      BYTE AllAreDefined
      if (AllAreDefined == 0)
      {
        for(NumFiles)
          BIT AttributesAreDefined
      }
      BYTE External;
      if(External != 0)
        UINT64 DataIndex
      []
      for(Definded Attributes)
        UINT32 Attributes
      []
  }
}
```

### Header

```text
BYTE NID::kHeader (0x01)

[]
ArchiveProperties
[]

[]
BYTE NID::kAdditionalStreamsInfo; (0x03)
StreamsInfo
[]

[]
BYTE NID::kMainStreamsInfo;    (0x04)
StreamsInfo
[]

[]
FilesInfo
[]

BYTE NID::kEnd
```

### HeaderInfo

```text
[]
BYTE NID::kEncodedHeader; (0x17)
StreamsInfo for Encoded Header
[]
```

---

End of document.
