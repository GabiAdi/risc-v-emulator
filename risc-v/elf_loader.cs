using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class ElfLoader
{
    private byte[] elfData;
    private uint entryPoint;
    private uint textStart;
    private uint textEnd;

    public ElfLoader(string path)
    {
        elfData = File.ReadAllBytes(path);
        ParseElfHeader();
    }

    private void ParseElfHeader()
    {
        // ELF32 Header
        if (elfData[0] != 0x7F || elfData[1] != 'E' || elfData[2] != 'L' || elfData[3] != 'F')
            throw new Exception("Not an ELF file");

        byte eiClass = elfData[4];
        if (eiClass != 1) throw new Exception("Not a 32-bit ELF");

        entryPoint = BitConverter.ToUInt32(elfData, 0x18);
        textStart = uint.MaxValue;
        textEnd = 0;

        // Parse Program Headers to find executable sections
        ushort phoff = BitConverter.ToUInt16(elfData, 0x1C); // Program header offset (32-bit)
        ushort phentsize = BitConverter.ToUInt16(elfData, 0x2A);
        ushort phnum = BitConverter.ToUInt16(elfData, 0x2C);

        for (int i = 0; i < phnum; i++)
        {
            int offset = (int)(phoff + i * phentsize);
            uint p_type = BitConverter.ToUInt32(elfData, offset);
            uint p_offset = BitConverter.ToUInt32(elfData, offset + 4);
            uint p_vaddr = BitConverter.ToUInt32(elfData, offset + 8);
            uint p_filesz = BitConverter.ToUInt32(elfData, offset + 16);
            uint p_flags = BitConverter.ToUInt32(elfData, offset + 24);

            const uint PF_X = 1; // Executable flag

            if ((p_flags & PF_X) != 0)
            {
                if (p_vaddr < textStart) textStart = p_vaddr;
                if (p_vaddr + p_filesz > textEnd) textEnd = p_vaddr + p_filesz;
            }
        }
    }

    public uint EntryPoint => entryPoint;
    public uint TextStart => textStart;
    public uint TextEnd => textEnd;

    public uint[] GetExecutableWords()
    {
        List<uint> words = new List<uint>();

        ushort phoff = BitConverter.ToUInt16(elfData, 0x1C);
        ushort phentsize = BitConverter.ToUInt16(elfData, 0x2A);
        ushort phnum = BitConverter.ToUInt16(elfData, 0x2C);

        const uint PF_X = 1; // Executable flag

        for (int i = 0; i < phnum; i++)
        {
            int offset = (int)(phoff + i * phentsize);
            uint p_flags = BitConverter.ToUInt32(elfData, offset + 24);

            if ((p_flags & PF_X) != 0)
            {
                uint p_offset = BitConverter.ToUInt32(elfData, offset + 4);
                uint p_filesz = BitConverter.ToUInt32(elfData, offset + 16);

                for (int j = 0; j < p_filesz; j += 4)
                {
                    uint word = BitConverter.ToUInt32(elfData, (int)(p_offset + j));
                    words.Add(word);
                }
            }
        }

        return words.ToArray();
    }

    private int RvaToOffset(uint vaddr)
    {
        // Map virtual address to file offset using program headers
        ushort phoff = BitConverter.ToUInt16(elfData, 0x1C); // Program header offset
        ushort phentsize = BitConverter.ToUInt16(elfData, 0x2A);
        ushort phnum = BitConverter.ToUInt16(elfData, 0x2C);

        for (int i = 0; i < phnum; i++)
        {
            int offset = (int)(phoff + i * phentsize);
            uint p_type = BitConverter.ToUInt32(elfData, offset);
            uint p_offset = BitConverter.ToUInt32(elfData, offset + 4);
            uint p_vaddr = BitConverter.ToUInt32(elfData, offset + 8);
            uint p_filesz = BitConverter.ToUInt32(elfData, offset + 16);

            if (vaddr >= p_vaddr && vaddr < p_vaddr + p_filesz)
                return (int)(p_offset + (vaddr - p_vaddr));
        }
        throw new Exception($"Cannot map virtual address 0x{vaddr:X} to file offset");
    }

    // ---------------- Symbol Parsing ----------------
    public Dictionary<uint, string> GetSymbols()
    {
        Dictionary<uint, string> symbols = new Dictionary<uint, string>();

        ushort shoff = BitConverter.ToUInt16(elfData, 0x20); // Section header offset
        ushort shentsize = BitConverter.ToUInt16(elfData, 0x2E);
        ushort shnum = BitConverter.ToUInt16(elfData, 0x30);
        ushort shstrndx = BitConverter.ToUInt16(elfData, 0x32); // Section header string table index

        // Read section header string table
        uint shstrOffset = BitConverter.ToUInt32(elfData, (int)(shoff + shstrndx * shentsize + 16));
        uint shstrSize = BitConverter.ToUInt32(elfData, (int)(shoff + shstrndx * shentsize + 20));
        byte[] shstr = new byte[shstrSize];
        Array.Copy(elfData, shstrOffset, shstr, 0, shstrSize);

        // Iterate section headers to find symbol tables
        for (int i = 0; i < shnum; i++)
        {
            int offset = (int)(shoff + i * shentsize);
            uint sh_name = BitConverter.ToUInt32(elfData, offset);
            uint sh_type = BitConverter.ToUInt32(elfData, offset + 4);
            uint sh_offset = BitConverter.ToUInt32(elfData, offset + 16);
            uint sh_size = BitConverter.ToUInt32(elfData, offset + 20);
            uint sh_entsize = BitConverter.ToUInt32(elfData, offset + 36);

            string sectionName = ReadString(shstr, sh_name);

            // Only parse SYMTAB sections
            if (sh_type == 2) // SHT_SYMTAB
            {
                // Find associated string table
                uint link = BitConverter.ToUInt32(elfData, offset + 24); // sh_link
                int strOffset = (int)BitConverter.ToUInt32(elfData, (int)(shoff + link * shentsize + 16));
                int strSize = (int)BitConverter.ToUInt32(elfData, (int)(shoff + link * shentsize + 20));
                byte[] strtab = new byte[strSize];
                Array.Copy(elfData, strOffset, strtab, 0, strSize);

                int numSymbols = (int)(sh_size / sh_entsize);
                for (int s = 0; s < numSymbols; s++)
                {
                    int symOffset = (int)(sh_offset + s * sh_entsize);
                    uint st_name = BitConverter.ToUInt32(elfData, symOffset);
                    uint st_value = BitConverter.ToUInt32(elfData, symOffset + 4);
                    // uint st_size = BitConverter.ToUInt32(elfData, symOffset + 8);
                    // byte st_info = elfData[symOffset + 12];

                    string name = ReadString(strtab, st_name);
                    if (!string.IsNullOrEmpty(name) && !symbols.ContainsKey(st_value))
                        symbols[st_value] = name;
                }
            }
        }

        return symbols;
    }

    private string ReadString(byte[] data, uint offset)
    {
        int i = (int)offset;
        List<byte> bytes = new List<byte>();
        while (i < data.Length && data[i] != 0)
        {
            bytes.Add(data[i]);
            i++;
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}
