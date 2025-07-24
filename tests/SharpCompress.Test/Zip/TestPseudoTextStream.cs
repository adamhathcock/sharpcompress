using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpCompress.Test.Zip;

/// <summary>
/// Generates pseudo English-style text for testing - Nanook
/// </summary>
internal class TestPseudoTextStream : Stream
{
    private static readonly char[] _vowels = { 'a', 'e', 'i', 'o', 'u' };
    private static readonly char[] _consonants = "bcdfghjklmnpqrstvwxyz".ToCharArray();

    private long _position = 0;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException();

    public static byte[] Create(int size)
    {
        byte[] data = new byte[size];
        using (TestPseudoTextStream rts = new TestPseudoTextStream())
        {
            int bufferSize = 64 * 1024; // 64k blocks
            byte[] buffer = new byte[bufferSize];
            int bytesRead = 0;
            int totalBytesRead = 0;

            while (totalBytesRead < size)
            {
                bytesRead = rts.Read(buffer, 0, Math.Min(bufferSize, size - totalBytesRead));
                Array.Copy(buffer, 0, data, totalBytesRead, bytesRead);
                totalBytesRead += bytesRead;
            }
        }

        return data;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int bytesRead = 0;
        while (bytesRead < count)
        {
            string word = GenerateDeterministicWord(_position + bytesRead);
            byte[] wordBytes = System.Text.Encoding.ASCII.GetBytes(word + " ");

            int bytesToCopy = Math.Min(wordBytes.Length, count - bytesRead);
            Array.Copy(wordBytes, 0, buffer, offset + bytesRead, bytesToCopy);

            bytesRead += bytesToCopy;
            _position += bytesToCopy;
        }

        return bytesRead;
    }

    private string GenerateDeterministicWord(long seed)
    {
        int length = (int)(seed % 7) + 2; // 2 to 8 letters
        int vowelCount = (int)((seed / 7) % 4) + 2; // 2 to 5 vowels

        System.Text.StringBuilder word = new System.Text.StringBuilder(length);
        int vowelsAdded = 0;

        for (int i = 0; i < length; i++)
        {
            if (vowelsAdded < vowelCount && ((seed >> i) & 1) == 0)
            {
                word.Append(_vowels[(int)(seed >> (i + 1)) % _vowels.Length]);
                vowelsAdded++;
            }
            else
            {
                word.Append(_consonants[(int)(seed >> (i + 1)) % _consonants.Length]);
            }
        }

        return word.ToString();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
