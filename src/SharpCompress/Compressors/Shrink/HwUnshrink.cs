using System;

namespace SharpCompress.Compressors.Shrink
{
    public class HwUnshrink
    {
        private const int MIN_CODE_SIZE = 9;
        private const int MAX_CODE_SIZE = 13;

        private const ushort MAX_CODE = (ushort)((1U << MAX_CODE_SIZE) - 1);
        private const ushort INVALID_CODE = ushort.MaxValue;
        private const ushort CONTROL_CODE = 256;
        private const ushort INC_CODE_SIZE = 1;
        private const ushort PARTIAL_CLEAR = 2;

        private const int HASH_BITS = MAX_CODE_SIZE + 1; // For a load factor of 0.5.
        private const int HASHTAB_SIZE = 1 << HASH_BITS;
        private const ushort UNKNOWN_LEN = ushort.MaxValue;

        private struct CodeTabEntry
        {
            public int prefixCode; // INVALID_CODE means the entry is invalid.
            public byte extByte;
            public ushort len;
            public int lastDstPos;
        }

        private static void CodeTabInit(CodeTabEntry[] codeTab)
        {
            for (var i = 0; i <= byte.MaxValue; i++)
            {
                codeTab[i].prefixCode = (ushort)i;
                codeTab[i].extByte = (byte)i;
                codeTab[i].len = 1;
            }

            for (var i = byte.MaxValue + 1; i <= MAX_CODE; i++)
            {
                codeTab[i].prefixCode = INVALID_CODE;
            }
        }

        private static void UnshrinkPartialClear(CodeTabEntry[] codeTab, ref CodeQueue queue)
        {
            var isPrefix = new bool[MAX_CODE + 1];
            int codeQueueSize;

            // Scan for codes that have been used as a prefix.
            for (var i = CONTROL_CODE + 1; i <= MAX_CODE; i++)
            {
                if (codeTab[i].prefixCode != INVALID_CODE)
                {
                    isPrefix[codeTab[i].prefixCode] = true;
                }
            }

            // Clear "non-prefix" codes in the table; populate the code queue.
            codeQueueSize = 0;
            for (var i = CONTROL_CODE + 1; i <= MAX_CODE; i++)
            {
                if (!isPrefix[i])
                {
                    codeTab[i].prefixCode = INVALID_CODE;
                    queue.codes[codeQueueSize++] = (ushort)i;
                }
            }

            queue.codes[codeQueueSize] = INVALID_CODE; // End-of-queue marker.
            queue.nextIdx = 0;
        }

        private static bool ReadCode(
            BitStream stream,
            ref int codeSize,
            CodeTabEntry[] codeTab,
            ref CodeQueue queue,
            out int nextCode
        )
        {
            int code,
                controlCode;

            code = (int)stream.NextBits(codeSize);
            if (!stream.Advance(codeSize))
            {
                nextCode = INVALID_CODE;
                return false;
            }

            // Handle regular codes (the common case).
            if (code != CONTROL_CODE)
            {
                nextCode = code;
                return true;
            }

            // Handle control codes.
            controlCode = (ushort)stream.NextBits(codeSize);
            if (!stream.Advance(codeSize))
            {
                nextCode = INVALID_CODE;
                return true;
            }

            if (controlCode == INC_CODE_SIZE && codeSize < MAX_CODE_SIZE)
            {
                codeSize++;
                return ReadCode(stream, ref codeSize, codeTab, ref queue, out nextCode);
            }

            if (controlCode == PARTIAL_CLEAR)
            {
                UnshrinkPartialClear(codeTab, ref queue);
                return ReadCode(stream, ref codeSize, codeTab, ref queue, out nextCode);
            }

            nextCode = INVALID_CODE;
            return true;
        }

        private static void CopyFromPrevPos(byte[] dst, int prevPos, int dstPos, int len)
        {
            if (dstPos + len > dst.Length)
            {
                // Not enough room in dst for the sloppy copy below.
                Array.Copy(dst, prevPos, dst, dstPos, len);
                return;
            }

            if (prevPos + len > dstPos)
            {
                // Benign one-byte overlap possible in the KwKwK case.
                //assert(prevPos + len == dstPos + 1);
                //assert(dst[prevPos] == dst[prevPos + len - 1]);
            }

            Buffer.BlockCopy(dst, prevPos, dst, dstPos, len);
        }

        private static UnshrnkStatus OutputCode(
            int code,
            byte[] dst,
            int dstPos,
            int dstCap,
            int prevCode,
            CodeTabEntry[] codeTab,
            ref CodeQueue queue,
            out byte firstByte,
            out int len
        )
        {
            int prefixCode;

            //assert(code <= MAX_CODE && code != CONTROL_CODE);
            //assert(dstPos < dstCap);
            firstByte = 0;
            if (code <= byte.MaxValue)
            {
                // Output literal byte.
                firstByte = (byte)code;
                len = 1;
                dst[dstPos] = (byte)code;
                return UnshrnkStatus.Ok;
            }

            if (codeTab[code].prefixCode == INVALID_CODE || codeTab[code].prefixCode == code)
            {
                // Reject invalid codes. Self-referential codes may exist in the table but cannot be used.
                firstByte = 0;
                len = 0;
                return UnshrnkStatus.Error;
            }

            if (codeTab[code].len != UNKNOWN_LEN)
            {
                // Output string with known length (the common case).
                if (dstCap - dstPos < codeTab[code].len)
                {
                    firstByte = 0;
                    len = 0;
                    return UnshrnkStatus.Full;
                }

                CopyFromPrevPos(dst, codeTab[code].lastDstPos, dstPos, codeTab[code].len);
                firstByte = dst[dstPos];
                len = codeTab[code].len;
                return UnshrnkStatus.Ok;
            }

            // Output a string of unknown length.
            //assert(codeTab[code].len == UNKNOWN_LEN);
            prefixCode = codeTab[code].prefixCode;
            // assert(prefixCode > CONTROL_CODE);

            if (prefixCode == queue.codes[queue.nextIdx])
            {
                // The prefix code hasn't been added yet, but we were just about to: the KwKwK case.
                //assert(codeTab[prevCode].prefixCode != INVALID_CODE);
                codeTab[prefixCode].prefixCode = prevCode;
                codeTab[prefixCode].extByte = firstByte;
                codeTab[prefixCode].len = (ushort)(codeTab[prevCode].len + 1);
                codeTab[prefixCode].lastDstPos = codeTab[prevCode].lastDstPos;
                dst[dstPos] = firstByte;
            }
            else if (codeTab[prefixCode].prefixCode == INVALID_CODE)
            {
                // The prefix code is still invalid.
                firstByte = 0;
                len = 0;
                return UnshrnkStatus.Error;
            }

            // Output the prefix string, then the extension byte.
            len = codeTab[prefixCode].len + 1;
            if (dstCap - dstPos < len)
            {
                firstByte = 0;
                len = 0;
                return UnshrnkStatus.Full;
            }

            CopyFromPrevPos(dst, codeTab[prefixCode].lastDstPos, dstPos, codeTab[prefixCode].len);
            dst[dstPos + len - 1] = codeTab[code].extByte;
            firstByte = dst[dstPos];

            // Update the code table now that the string has a length and pos.
            //assert(prevCode != code);
            codeTab[code].len = (ushort)len;
            codeTab[code].lastDstPos = dstPos;

            return UnshrnkStatus.Ok;
        }

        public static UnshrnkStatus Unshrink(
            byte[] src,
            int srcLen,
            out int srcUsed,
            byte[] dst,
            int dstCap,
            out int dstUsed
        )
        {
            var codeTab = new CodeTabEntry[HASHTAB_SIZE];
            var queue = new CodeQueue();
            var stream = new BitStream(src, srcLen);
            int codeSize,
                dstPos,
                len;
            int currCode,
                prevCode,
                newCode;
            byte firstByte;

            CodeTabInit(codeTab);
            CodeQueueInit(ref queue);
            codeSize = MIN_CODE_SIZE;
            dstPos = 0;

            // Handle the first code separately since there is no previous code.
            if (!ReadCode(stream, ref codeSize, codeTab, ref queue, out currCode))
            {
                srcUsed = stream.BytesRead;
                dstUsed = 0;
                return UnshrnkStatus.Ok;
            }

            //assert(currCode != CONTROL_CODE);
            if (currCode > byte.MaxValue)
            {
                srcUsed = stream.BytesRead;
                dstUsed = 0;
                return UnshrnkStatus.Error; // The first code must be a literal.
            }

            if (dstPos == dstCap)
            {
                srcUsed = stream.BytesRead;
                dstUsed = 0;
                return UnshrnkStatus.Full;
            }

            firstByte = (byte)currCode;
            dst[dstPos] = (byte)currCode;
            codeTab[currCode].lastDstPos = dstPos;
            dstPos++;

            prevCode = currCode;
            while (ReadCode(stream, ref codeSize, codeTab, ref queue, out currCode))
            {
                if (currCode == INVALID_CODE)
                {
                    srcUsed = stream.BytesRead;
                    dstUsed = 0;
                    return UnshrnkStatus.Error;
                }

                if (dstPos == dstCap)
                {
                    srcUsed = stream.BytesRead;
                    dstUsed = 0;
                    return UnshrnkStatus.Full;
                }

                // Handle KwKwK: next code used before being added.
                if (currCode == queue.codes[queue.nextIdx])
                {
                    if (codeTab[prevCode].prefixCode == INVALID_CODE)
                    {
                        // The previous code is no longer valid.
                        srcUsed = stream.BytesRead;
                        dstUsed = 0;
                        return UnshrnkStatus.Error;
                    }

                    // Extend the previous code with its first byte.
                    //assert(currCode != prevCode);
                    codeTab[currCode].prefixCode = prevCode;
                    codeTab[currCode].extByte = firstByte;
                    codeTab[currCode].len = (ushort)(codeTab[prevCode].len + 1);
                    codeTab[currCode].lastDstPos = codeTab[prevCode].lastDstPos;
                    //assert(dstPos < dstCap);
                    dst[dstPos] = firstByte;
                }

                // Output the string represented by the current code.
                var status = OutputCode(
                    currCode,
                    dst,
                    dstPos,
                    dstCap,
                    prevCode,
                    codeTab,
                    ref queue,
                    out firstByte,
                    out len
                );
                if (status != UnshrnkStatus.Ok)
                {
                    srcUsed = stream.BytesRead;
                    dstUsed = 0;
                    return status;
                }

                // Verify that the output matches walking the prefixes.
                var c = currCode;
                for (var i = 0; i < len; i++)
                {
                    // assert(codeTab[c].len == len - i);
                    //assert(codeTab[c].extByte == dst[dstPos + len - i - 1]);
                    c = codeTab[c].prefixCode;
                }

                // Add a new code to the string table if there's room.
                // The string is the previous code's string extended with the first byte of the current code's string.
                newCode = CodeQueueRemoveNext(ref queue);
                if (newCode != INVALID_CODE)
                {
                    //assert(codeTab[prevCode].lastDstPos < dstPos);
                    codeTab[newCode].prefixCode = prevCode;
                    codeTab[newCode].extByte = firstByte;
                    codeTab[newCode].len = (ushort)(codeTab[prevCode].len + 1);
                    codeTab[newCode].lastDstPos = codeTab[prevCode].lastDstPos;

                    if (codeTab[prevCode].prefixCode == INVALID_CODE)
                    {
                        // prevCode was invalidated in a partial clearing. Until that code is re-used, the
                        // string represented by newCode is indeterminate.
                        codeTab[newCode].len = UNKNOWN_LEN;
                    }
                    // If prevCode was invalidated in a partial clearing, it's possible that newCode == prevCode,
                    // in which case it will never be used or cleared.
                }

                codeTab[currCode].lastDstPos = dstPos;
                dstPos += len;

                prevCode = currCode;
            }

            srcUsed = stream.BytesRead;
            dstUsed = dstPos;

            return UnshrnkStatus.Ok;
        }

        public enum UnshrnkStatus
        {
            Ok,
            Full,
            Error,
        }

        private struct CodeQueue
        {
            public int nextIdx;
            public ushort[] codes;
        }

        private static void CodeQueueInit(ref CodeQueue q)
        {
            int codeQueueSize;
            ushort code;

            codeQueueSize = 0;
            q.codes = new ushort[MAX_CODE - CONTROL_CODE + 2];

            for (code = CONTROL_CODE + 1; code <= MAX_CODE; code++)
            {
                q.codes[codeQueueSize++] = code;
            }

            //assert(codeQueueSize < q.codes.Length);
            q.codes[codeQueueSize] = INVALID_CODE; // End-of-queue marker.
            q.nextIdx = 0;
        }

        private static ushort CodeQueueNext(ref CodeQueue q) =>
            //assert(q.nextIdx < q.codes.Length);
            q.codes[q.nextIdx];

        private static ushort CodeQueueRemoveNext(ref CodeQueue q)
        {
            var code = CodeQueueNext(ref q);
            if (code != INVALID_CODE)
            {
                q.nextIdx++;
            }
            return code;
        }
    }
}
