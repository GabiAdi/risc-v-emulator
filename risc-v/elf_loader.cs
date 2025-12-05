using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ElfLoader
{
    public class ExecRegion
    {
        public uint Start;
        public uint End;
        public uint Size => End - Start;
        public override string ToString() => $"[0x{Start:X} - 0x{End:X}] ({Size} bytes)";
    }

    public uint EntryPoint { get; private set; }
    public ExecRegion ExecutableRegion { get; private set; }
    public List<ExecRegion> AllExecutableRegions { get; } = new();

    private byte[] elfData;

    private const ushort EM_RISCV = 243;
    private const byte ELFCLASS32 = 1;
    private const byte ELFDATA2LSB = 1;
    private const uint PT_LOAD = 1;
    private const uint PF_X = 0x1;
    
    public ElfLoader(string path)
    {
        Load(path);
    }

    public void Load(string path)
    {
        elfData = File.ReadAllBytes(path);

        using var ms = new MemoryStream(elfData);
        using var br = new BinaryReader(ms);

        // ================= ELF HEADER ===================
        byte[] magic = br.ReadBytes(4);
        if (magic[0] != 0x7F || magic[1] != 'E' || magic[2] != 'L' || magic[3] != 'F')
            throw new Exception("Not an ELF file");

        if (br.ReadByte() != ELFCLASS32) throw new Exception("Not ELF32");
        if (br.ReadByte() != ELFDATA2LSB) throw new Exception("File not little-endian");

        br.BaseStream.Position = 0x10; // e_machine offset in ELF32
        ushort machine = br.ReadUInt16();
        // if (machine != EM_RISCV) throw new Exception("Not a RISC-V ELF file");

        br.BaseStream.Position = 0x18; // e_entry offset
        EntryPoint = br.ReadUInt32();

        br.BaseStream.Position = 0x1C; // e_phoff
        uint phoff = br.ReadUInt32();

        br.BaseStream.Position = 0x2A; // e_phentsize
        ushort phentsize = br.ReadUInt16();
        ushort phnum = br.ReadUInt16();

        // ================= PROGRAM HEADERS ===================
        br.BaseStream.Position = phoff;

        for (int i = 0; i < phnum; i++)
        {
            uint type = br.ReadUInt32();
            uint offset = br.ReadUInt32();
            uint vaddr  = br.ReadUInt32();
            uint paddr  = br.ReadUInt32();
            uint filesz = br.ReadUInt32();
            uint memsz  = br.ReadUInt32();
            uint flags  = br.ReadUInt32();
            uint align  = br.ReadUInt32();

            // Only LOAD segments with execute permission
            if (type == PT_LOAD && (flags & PF_X) != 0)
            {
                var region = new ExecRegion
                {
                    Start = vaddr,
                    End = vaddr + memsz
                };

                AllExecutableRegions.Add(region);
            }
        }

        if (AllExecutableRegions.Count == 0)
            throw new Exception("No executable segments found");

        ExecutableRegion = new ExecRegion
        {
            Start = AllExecutableRegions.Min(r => r.Start),
            End = AllExecutableRegions.Max(r => r.End)
        };
    }

    /// <summary>
    /// Returns the executable region as an array of 32-bit words (uint).
    /// </summary>
    public uint[] GetExecutableWords()
    {
        if (elfData == null)
            throw new Exception("ELF not loaded");

        var words = new List<uint>();

        using var ms = new MemoryStream(elfData);
        using var br = new BinaryReader(ms);

        // Re-read program headers
        br.BaseStream.Position = 0x1C; // e_phoff
        uint phoff = br.ReadUInt32();
        br.BaseStream.Position = 0x2A; // e_phnum
        ushort phnum = br.ReadUInt16();
        br.BaseStream.Position = phoff;

        for (int i = 0; i < phnum; i++)
        {
            uint type = br.ReadUInt32();
            uint offset = br.ReadUInt32();
            uint vaddr  = br.ReadUInt32();
            uint paddr  = br.ReadUInt32();
            uint filesz = br.ReadUInt32();
            uint memsz  = br.ReadUInt32();
            uint flags  = br.ReadUInt32();
            uint align  = br.ReadUInt32();

            if (type == PT_LOAD && (flags & PF_X) != 0)
            {
                for (uint j = 0; j + 4 <= filesz; j += 4)
                {
                    uint word = BitConverter.ToUInt32(elfData, (int)(offset + j));
                    words.Add(word);
                }
            }
        }

        return words.ToArray();
    }
}
