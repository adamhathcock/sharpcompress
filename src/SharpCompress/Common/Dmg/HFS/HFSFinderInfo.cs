using System;

namespace SharpCompress.Common.Dmg.HFS
{
    internal struct HFSPoint
    {
        public short V;
        public short H;
    }

    internal struct HFSRect
    {
        public short Top;
        public short Left;
        public short Bottom;
        public short Right;
    }

    [Flags]
    internal enum HFSFinderFlags : ushort
    {
        None = 0x0000,

        IsOnDesk = 0x0001,      /* Files and folders (System 6) */
        Color = 0x000E,         /* Files and folders */
        IsShared = 0x0040,      /* Files only (Applications only) If */
                                /* clear, the application needs */
                                /* to write to its resource fork, */
                                /* and therefore cannot be shared */
                                /* on a server */
        HasNoINITs = 0x0080,    /* Files only (Extensions/Control */
                                /* Panels only) */
                                /* This file contains no INIT resource */
        HasBeenInited = 0x0100, /* Files only.  Clear if the file */
                                /* contains desktop database resources */
                                /* ('BNDL', 'FREF', 'open', 'kind'...) */
                                /* that have not been added yet.  Set */
                                /* only by the Finder. */
                                /* Reserved for folders */
        HasCustomIcon = 0x0400, /* Files and folders */
        IsStationery = 0x0800,  /* Files only */
        NameLocked = 0x1000,    /* Files and folders */
        HasBundle = 0x2000,     /* Files only */
        IsInvisible = 0x4000,   /* Files and folders */
        IsAlias = 0x8000        /* Files only */
    }

    [Flags]
    internal enum HFSExtendedFinderFlags : ushort
    {
        None = 0x0000,

        ExtendedFlagsAreInvalid = 0x8000, /* The other extended flags */
                                          /* should be ignored */
        HasCustomBadge = 0x0100,          /* The file or folder has a */
                                          /* badge resource */
        HasRoutingInfo = 0x0004           /* The file contains routing */
                                          /* info resource */
    }

    internal sealed class HFSFileInfo : HFSStructBase
    {
        public string FileType { get; }             /* The type of the file */
        public string FileCreator { get; }          /* The file's creator */
        public HFSFinderFlags FinderFlags { get; }
        public HFSPoint Location { get; }           /* File's location in the folder. */
        public HFSExtendedFinderFlags ExtendedFinderFlags { get; }
        public int PutAwayFolderId { get; }

        private HFSFileInfo(
            string fileType,
            string fileCreator,
            HFSFinderFlags finderFlags,
            HFSPoint location,
            HFSExtendedFinderFlags extendedFinderFlags,
            int putAwayFolderId)
        {
            FileType = fileType;
            FileCreator = fileCreator;
            FinderFlags = finderFlags;
            Location = location;
            ExtendedFinderFlags = extendedFinderFlags;
            PutAwayFolderId = putAwayFolderId;
        }

        public static HFSFileInfo Read(ref ReadOnlySpan<byte> data)
        {
            string fileType = ReadOSType(ref data);
            string fileCreator = ReadOSType(ref data);
            var finderFlags = (HFSFinderFlags)ReadUInt16(ref data);
            var location = ReadPoint(ref data);
            _ = ReadUInt16(ref data); // reserved
            data = data.Slice(4 * sizeof(short)); // reserved
            var extendedFinderFlags = (HFSExtendedFinderFlags)ReadUInt16(ref data);
            _ = ReadInt16(ref data); // reserved
            int putAwayFolderId = ReadInt32(ref data);

            return new HFSFileInfo(fileType, fileCreator, finderFlags, location, extendedFinderFlags, putAwayFolderId);
        }
    }

    internal sealed class HFSFolderInfo : HFSStructBase
    {
        public HFSRect WindowBounds { get; }    /* The position and dimension of the */
                                                /* folder's window */
        public HFSFinderFlags FinderFlags { get; }
        public HFSPoint Location { get; }       /* Folder's location in the parent */
                                                /* folder. If set to {0, 0}, the Finder */
                                                /* will place the item automatically */
        public HFSPoint ScrollPosition { get; } /* Scroll position (for icon views) */
        public HFSExtendedFinderFlags ExtendedFinderFlags { get; }
        public int PutAwayFolderId { get; }

        private HFSFolderInfo(
            HFSRect windowBounds,
            HFSFinderFlags finderFlags,
            HFSPoint location,
            HFSPoint scrollPosition,
            HFSExtendedFinderFlags extendedFinderFlags,
            int putAwayFolderId)
        {
            WindowBounds = windowBounds;
            FinderFlags = finderFlags;
            Location = location;
            ScrollPosition = scrollPosition;
            ExtendedFinderFlags = extendedFinderFlags;
            PutAwayFolderId = putAwayFolderId;
        }

        public static HFSFolderInfo Read(ref ReadOnlySpan<byte> data)
        {
            var windowBounds = ReadRect(ref data);
            var finderFlags = (HFSFinderFlags)ReadUInt16(ref data);
            var location = ReadPoint(ref data);
            _ = ReadUInt16(ref data); // reserved
            var scrollPosition = ReadPoint(ref data);
            _ = ReadInt32(ref data); // reserved
            var extendedFinderFlags = (HFSExtendedFinderFlags)ReadUInt16(ref data);
            _ = ReadInt16(ref data); // reserved
            int putAwayFolderId = ReadInt32(ref data);

            return new HFSFolderInfo(windowBounds, finderFlags, location, scrollPosition, extendedFinderFlags, putAwayFolderId);
        }
    }
}
