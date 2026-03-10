using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class Fat16Reader : IDisposable
{
    // ── Public types ─────────────────────────────────────────────────────────

    public class BiosParameterBlock
    {
        public string  OemName            { get; init; }
        public ushort  BytesPerSector     { get; init; }
        public byte    SectorsPerCluster  { get; init; }
        public ushort  ReservedSectors    { get; init; }
        public byte    NumberOfFATs       { get; init; }
        public ushort  RootEntryCount     { get; init; }
        public uint    TotalSectors       { get; init; }
        public byte    MediaDescriptor    { get; init; }
        public ushort  SectorsPerFAT      { get; init; }
        public ushort  SectorsPerTrack    { get; init; }
        public ushort  NumberOfHeads      { get; init; }
        public uint    HiddenSectors      { get; init; }
        public byte    DriveNumber        { get; init; }
        public byte    BootSignature      { get; init; }
        public uint    VolumeID           { get; init; }
        public string  VolumeLabel        { get; init; }
        public string  FileSystemType     { get; init; }
    }

    public class FileEntry
    {
        public string   ShortName      { get; init; }   // e.g. "HELLO"
        public string   Extension      { get; init; }   // e.g. "TXT"
        public string   FullName       { get; init; }   // e.g. "HELLO.TXT"
        public byte     Attributes     { get; init; }
        public ushort   FirstCluster   { get; init; }
        public uint     FileSize       { get; init; }
        public uint     FirstSector    { get; init; }
        public uint     ByteOffset     { get; init; }
        public IReadOnlyList<ushort> ClusterChain { get; init; }
        public bool     IsDirectory    => (Attributes & ATTR_DIRECTORY) != 0;
        public bool     IsVolumeLabel  => (Attributes & ATTR_VOLUME_ID) != 0;
        public bool     IsReadOnly     => (Attributes & ATTR_READ_ONLY) != 0;
        public bool     IsHidden       => (Attributes & ATTR_HIDDEN)    != 0;
        public bool     IsSystem       => (Attributes & ATTR_SYSTEM)    != 0;
    }

    // ── Attribute constants ──────────────────────────────────────────────────

    public const byte ATTR_READ_ONLY = 0x01;
    public const byte ATTR_HIDDEN    = 0x02;
    public const byte ATTR_SYSTEM    = 0x04;
    public const byte ATTR_VOLUME_ID = 0x08;
    public const byte ATTR_DIRECTORY = 0x10;
    public const byte ATTR_ARCHIVE   = 0x20;
    private const byte ATTR_LFN      = 0x0F;

    // ── Public properties ────────────────────────────────────────────────────

    public BiosParameterBlock BPB               { get; private set; }
    public uint               FatStartSector     { get; private set; }
    public uint               RootDirStartSector { get; private set; }
    public uint               DataStartSector    { get; private set; }
    public uint               BytesPerCluster    { get; private set; }
    public uint               TotalDataClusters  { get; private set; }

    // ── Private state ────────────────────────────────────────────────────────

    private readonly byte[] _image;

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <summary>Loads a FAT16 disk image from <paramref name="imagePath"/>.</summary>
    public Fat16Reader(string imagePath)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Disk image not found.", imagePath);

        _image = File.ReadAllBytes(imagePath);
        ParseBPB();
        ComputeLayout();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all valid (non-deleted, non-LFN) entries in the root directory.
    /// </summary>
    public IReadOnlyList<FileEntry> GetRootDirectoryEntries()
    {
        var entries = new List<FileEntry>();
        uint offset = RootDirStartSector * BPB.BytesPerSector;

        for (int i = 0; i < BPB.RootEntryCount; i++)
        {
            uint pos = offset + (uint)(i * 32);
            if (pos + 32 > _image.Length) break;

            byte first = _image[pos];
            if (first == 0x00) break;    // end of directory
            if (first == 0xE5) continue; // deleted entry

            byte attr = _image[pos + 11];
            if (attr == ATTR_LFN) continue; // long file name entry

            entries.Add(ParseEntry(pos));
        }

        return entries;
    }

    /// <summary>
    /// Searches the root directory for a file by name (case-insensitive).
    /// Returns <c>null</c> if not found.
    /// </summary>
    public FileEntry FindFile(string filename)
    {
        (string name8, string ext3) = ToShortNameParts(filename);

        uint offset = RootDirStartSector * BPB.BytesPerSector;
        for (int i = 0; i < BPB.RootEntryCount; i++)
        {
            uint pos = offset + (uint)(i * 32);
            if (pos + 32 > _image.Length) break;

            byte first = _image[pos];
            if (first == 0x00) break;
            if (first == 0xE5) continue;

            byte attr = _image[pos + 11];
            if (attr == ATTR_LFN) continue;

            string entName = AsciiString(pos,     8);
            string entExt  = AsciiString(pos + 8, 3);

            if (entName == name8 && entExt == ext3)
                return ParseEntry(pos);
        }

        return null;
    }

    /// <summary>
    /// Returns the raw bytes of a file identified by its <see cref="FileEntry"/>.
    /// </summary>
    public byte[] ReadFile(FileEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));

        var result = new List<byte>((int)entry.FileSize);

        foreach (ushort cluster in entry.ClusterChain)
        {
            uint sector = ClusterToSector(cluster);
            uint bytePos = sector * BPB.BytesPerSector;
            int  toCopy  = (int)Math.Min(BytesPerCluster, (uint)(_image.Length - bytePos));
            if (toCopy <= 0) break;
            result.AddRange(new ArraySegment<byte>(_image, (int)bytePos, toCopy));
        }

        // Trim to actual file size
        if (result.Count > (int)entry.FileSize)
            result.RemoveRange((int)entry.FileSize, result.Count - (int)entry.FileSize);

        return result.ToArray();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void ParseBPB()
    {
        ushort total16 = ReadU16(19);
        uint   total32 = ReadU32(32);

        BPB = new BiosParameterBlock
        {
            OemName           = Encoding.ASCII.GetString(_image, 3, 8).TrimEnd(),
            BytesPerSector    = ReadU16(11),
            SectorsPerCluster = _image[13],
            ReservedSectors   = ReadU16(14),
            NumberOfFATs      = _image[16],
            RootEntryCount    = ReadU16(17),
            TotalSectors      = total16 != 0 ? total16 : total32,
            MediaDescriptor   = _image[21],
            SectorsPerFAT     = ReadU16(22),
            SectorsPerTrack   = ReadU16(24),
            NumberOfHeads     = ReadU16(26),
            HiddenSectors     = ReadU32(28),
            DriveNumber       = _image[36],
            BootSignature     = _image[38],
            VolumeID          = ReadU32(39),
            VolumeLabel       = Encoding.ASCII.GetString(_image, 43, 11).TrimEnd(),
            FileSystemType    = Encoding.ASCII.GetString(_image, 54, 8).TrimEnd(),
        };
    }

    private void ComputeLayout()
    {
        uint rootDirSectors = ((uint)(BPB.RootEntryCount * 32) + BPB.BytesPerSector - 1)
                              / BPB.BytesPerSector;

        FatStartSector     = BPB.ReservedSectors;
        RootDirStartSector = FatStartSector + (uint)(BPB.NumberOfFATs * BPB.SectorsPerFAT);
        DataStartSector    = RootDirStartSector + rootDirSectors;
        BytesPerCluster    = (uint)(BPB.BytesPerSector * BPB.SectorsPerCluster);
        TotalDataClusters  = (BPB.TotalSectors - DataStartSector) / BPB.SectorsPerCluster;
    }

    private FileEntry ParseEntry(uint pos)
    {
        string name = AsciiString(pos,     8).TrimEnd();
        string ext  = AsciiString(pos + 8, 3).TrimEnd();
        string full = string.IsNullOrEmpty(ext) ? name : $"{name}.{ext}";

        byte   attr         = _image[pos + 11];
        ushort firstCluster = ReadU16((int)(pos + 26));
        uint   fileSize     = ReadU32((int)(pos + 28));
        uint   firstSector  = ClusterToSector(firstCluster);

        return new FileEntry
        {
            ShortName    = name,
            Extension    = ext,
            FullName     = full,
            Attributes   = attr,
            FirstCluster = firstCluster,
            FileSize     = fileSize,
            FirstSector  = firstSector,
            ByteOffset   = firstSector * BPB.BytesPerSector,
            ClusterChain = GetClusterChain(firstCluster),
        };
    }

    private uint ClusterToSector(ushort cluster)
        => DataStartSector + (uint)((cluster - 2) * BPB.SectorsPerCluster);

    private IReadOnlyList<ushort> GetClusterChain(ushort startCluster)
    {
        var chain = new List<ushort>();
        ushort cluster = startCluster;

        while (cluster >= 0x0002 && cluster <= 0xFFEF)
        {
            chain.Add(cluster);
            if (chain.Count > TotalDataClusters + 1) break; // guard against corrupt chains

            uint fatOffset = FatStartSector * BPB.BytesPerSector + (uint)(cluster * 2);
            if (fatOffset + 2 > _image.Length) break;
            cluster = ReadU16((int)fatOffset);
        }

        return chain;
    }

    private string AsciiString(uint offset, int length)
        => Encoding.ASCII.GetString(_image, (int)offset, length);

    private ushort ReadU16(int offset)
        => (ushort)(_image[offset] | (_image[offset + 1] << 8));

    private uint ReadU32(int offset)
        => (uint)(_image[offset]
                | (_image[offset + 1] << 8)
                | (_image[offset + 2] << 16)
                | (_image[offset + 3] << 24));

    private static (string name8, string ext3) ToShortNameParts(string filename)
    {
        filename = filename.ToUpperInvariant();
        int dot = filename.IndexOf('.');
        string name8, ext3;

        if (dot >= 0)
        {
            name8 = filename.Substring(0, dot).PadRight(8).Substring(0, 8);
            ext3  = filename.Substring(dot + 1).PadRight(3).Substring(0, 3);
        }
        else
        {
            name8 = filename.PadRight(8).Substring(0, 8);
            ext3  = "   ";
        }

        return (name8, ext3);
    }

    public void Dispose() { /* image is in memory; nothing to release */ }
}

// ── Example usage (Program.cs) ───────────────────────────────────────────────
