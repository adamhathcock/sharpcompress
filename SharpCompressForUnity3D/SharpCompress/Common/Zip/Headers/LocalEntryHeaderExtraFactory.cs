namespace SharpCompress.Common.Zip.Headers
{
    using System;

    internal static class LocalEntryHeaderExtraFactory
    {
        internal static ExtraData Create(ExtraDataType type, ushort length, byte[] extraData)
        {
            if (type == ExtraDataType.UnicodePathExtraField)
            {
                ExtraUnicodePathExtraField field = new ExtraUnicodePathExtraField();
                field.Type = type;
                field.Length = length;
                field.DataBytes = extraData;
                return field;
            }
            ExtraData data = new ExtraData();
            data.Type = type;
            data.Length = length;
            data.DataBytes = extraData;
            return data;
        }
    }
}

