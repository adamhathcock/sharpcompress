# Large Test Archives

Each fixture contains one `large.bin` entry with a 67,108,864-byte (64 MiB)
deterministic, repeated text pattern with a 2 KiB `0xFF` prefix. The entry CRC-32 is
`f9081eb0`.

| Fixture | Archive API | Reader API |
| --- | --- | --- |
| `Large.zip` | Yes | Yes |
| Generated `Large.tar` | Yes | N/A |
| `Large.gz` | Yes | Yes |
| `Large.rar` | Yes | Yes |
| `Large.7z` | Yes | No |
| `Large.tar.gz` | No | Yes |

The compressible payload keeps the compressed fixtures small while requiring a full
64 MiB decompression to validate each API. The Archive API test expands `Large.tar.gz`
to a scratch `Large.tar` before exercising raw TAR support, so the 64 MiB TAR file is
not committed.

## Regenerating

The fixtures were created with `zip`, `tar`, `gzip`, RAR 7.22, and 7-Zip. Run these
commands from a temporary directory after replacing `<repo>` with the repository root:

```sh
yes "SharpCompress large fixture" | head -c 67108864 > large.bin
printf '\377%.0s' {1..2048} > prefix.bin
dd if=prefix.bin of=large.bin bs=2048 count=1 conv=notrunc
touch -t 202001010000 large.bin
mkdir -p <repo>/tests/TestArchives/Archives/Large
zip -X -9 -j <repo>/tests/TestArchives/Archives/Large/Large.zip large.bin
COPYFILE_DISABLE=1 tar -cf large.tar large.bin
gzip -9 -c large.bin > <repo>/tests/TestArchives/Archives/Large/Large.gz
gzip -n -9 -c large.tar > <repo>/tests/TestArchives/Archives/Large/Large.tar.gz
rar a -ma5 -m5 -ep <repo>/tests/TestArchives/Archives/Large/Large.rar large.bin
7z a -t7z -mx=9 <repo>/tests/TestArchives/Archives/Large/Large.7z large.bin
```
