using SharpCompress.Common.Dmg.Headers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace SharpCompress.Common.Dmg
{
    internal static class DmgUtil
    {
        private const string MalformedXmlMessage = "Malformed XML block";

        private static T[] ParseArray<T>(in XElement parent, in Func<XElement, T> parseElement)
        {
            var list = new List<T>();

            foreach (var node in parent.Elements())
                list.Add(parseElement(node));

            return list.ToArray();
        }

        private static Dictionary<string, T> ParseDict<T>(in XElement parent, in Func<XElement, T> parseValue)
        {
            var dict = new Dictionary<string, T>();

            string? key = null;
            foreach (var node in parent.Elements())
            {
                if (string.Equals(node.Name.LocalName, "key", StringComparison.Ordinal))
                {
                    key = node.Value;
                }
                else if (key is not null)
                {
                    var value = parseValue(node);
                    dict.Add(key, value);
                    key = null;
                }
            }

            return dict;
        }

        private static Dictionary<string, Dictionary<string, Dictionary<string, string>[]>> ParsePList(in XDocument doc)
        {
            var dictNode = doc.Root?.Element("dict");
            if (dictNode is null) throw new InvalidFormatException(MalformedXmlMessage);

            static Dictionary<string, string> ParseObject(XElement parent)
                => ParseDict(parent, node => node.Value);

            static Dictionary<string, string>[] ParseObjectArray(XElement parent)
                => ParseArray(parent, ParseObject);

            static Dictionary<string, Dictionary<string, string>[]> ParseSubDict(XElement parent)
                => ParseDict(parent, ParseObjectArray);

            return ParseDict(dictNode, ParseSubDict);
        }

        private static BlkxData CreateDataFromDict(in Dictionary<string, string> dict)
        {
            static bool TryParseHex(string? s, out uint value)
            {
                value = 0;
                if (string.IsNullOrEmpty(s)) return false;

                if (s!.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    s = s.Substring(2);

                return uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            if (!dict.TryGetValue("ID", out string? idStr) || !int.TryParse(idStr, out int id))
                throw new InvalidFormatException(MalformedXmlMessage);
            if (!dict.TryGetValue("Name", out string? name))
                throw new InvalidFormatException(MalformedXmlMessage);
            if (!dict.TryGetValue("Attributes", out string? attribStr) || !TryParseHex(attribStr, out uint attribs))
                throw new InvalidFormatException(MalformedXmlMessage);
            if (!dict.TryGetValue("Data", out string? base64Data) || string.IsNullOrEmpty(base64Data))
                throw new InvalidFormatException(MalformedXmlMessage);

            try
            {
                var data = Convert.FromBase64String(base64Data);
                if (!BlkxTable.TryRead(data, out var table))
                    throw new InvalidFormatException("Invalid BLKX table");

                return new BlkxData(id, name, attribs, table!);
            }
            catch (FormatException ex)
            {
                throw new InvalidFormatException(MalformedXmlMessage, ex);
            }
        }

        public static DmgBlockDataStream? LoadHFSPartitionStream(Stream baseStream, DmgHeader header)
        {
            if ((header.XMLOffset + header.XMLLength) >= (ulong)baseStream.Length)
                throw new IncompleteArchiveException("XML block incomplete");
            if ((header.DataForkOffset + header.DataForkLength) >= (ulong)baseStream.Length)
                throw new IncompleteArchiveException("Data block incomplete");

            baseStream.Position = (long)header.XMLOffset;
            var xmlBuffer = new byte[header.XMLLength];
            baseStream.Read(xmlBuffer, 0, (int)header.XMLLength);
            var xml = Encoding.ASCII.GetString(xmlBuffer);

            var doc = XDocument.Parse(xml);
            var pList = ParsePList(doc);
            if (!pList.TryGetValue("resource-fork", out var resDict) || !resDict.TryGetValue("blkx", out var blkxDicts))
                throw new InvalidFormatException(MalformedXmlMessage);

            var objs = new BlkxData[blkxDicts.Length];
            for (int i = 0; i < objs.Length; i++)
                objs[i] = CreateDataFromDict(blkxDicts[i]);

            // Index 0 is the protective MBR partition
            // Index 1 is the GPT header
            // Index 2 is the GPT partition table

            try
            {
                var headerData = objs[1];
                using var headerStream = new DmgBlockDataStream(baseStream, header, headerData.Table);
                if (!GptHeader.TryRead(headerStream, out var gptHeader))
                    throw new InvalidFormatException("Invalid GPT header");

                var tableData = objs[2];
                using var tableStream = new DmgBlockDataStream(baseStream, header, tableData.Table);
                var gptTable = new GptPartitionEntry[gptHeader!.EntriesCount];
                for (int i = 0; i < gptHeader.EntriesCount; i++)
                    gptTable[i] = GptPartitionEntry.Read(tableStream);

                foreach (var entry in gptTable)
                {
                    if (entry.TypeGuid == PartitionFormat.AppleHFS)
                    {
                        BlkxData? partitionData = null;
                        for (int i = 3; i < objs.Length; i++)
                        {
                            if (objs[i].Name.StartsWith(entry.Name, StringComparison.Ordinal))
                            {
                                partitionData = objs[i];
                                break;
                            }
                        }

                        if (partitionData is null)
                            throw new InvalidFormatException($"Missing partition {entry.Name}");

                        return new DmgBlockDataStream(baseStream, header, partitionData.Table);
                    }
                }

                return null;
            }
            catch (EndOfStreamException ex)
            {
                throw new IncompleteArchiveException("Partition incomplete", ex);
            }
        }

        private sealed class BlkxData
        {
            public int Id { get; }
            public string Name { get; }
            public uint Attributes { get; }
            public BlkxTable Table { get; }

            public BlkxData(int id, string name, uint attributes, BlkxTable table)
            {
                Id = id;
                Name = name;
                Attributes = attributes;
                Table = table;
            }
        }
    }
}
