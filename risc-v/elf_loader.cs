using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using risc_v;

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
    
    public uint GetFirstExecutableAddress()
    {
        // 1. Get entry point from ELF header
        uint entryPoint = BitConverter.ToUInt32(elfData, 0x18);
    
        // 2. Find program headers
        ushort phoff = BitConverter.ToUInt16(elfData, 0x1C);
        ushort phentsize = BitConverter.ToUInt16(elfData, 0x2A);
        ushort phnum = BitConverter.ToUInt16(elfData, 0x2C);

        // 3. Find which segment contains the entry point
        for (int i = 0; i < phnum; i++)
        {
            int headerOffset = (int)(phoff + i * phentsize);
        
            // Read segment fields
            uint p_type = BitConverter.ToUInt32(elfData, headerOffset + 0);
            uint p_offset = BitConverter.ToUInt32(elfData, headerOffset + 4);
            uint p_vaddr = BitConverter.ToUInt32(elfData, headerOffset + 8);
            uint p_filesz = BitConverter.ToUInt32(elfData, headerOffset + 16);
            uint p_flags = BitConverter.ToUInt32(elfData, headerOffset + 24);
        
            const uint PT_LOAD = 1; // Loadable segment
            const uint PF_X = 1;    // Executable flag
        
            // Check if it's a loadable, executable segment containing entry point
            if (p_type == PT_LOAD && 
                (p_flags & PF_X) != 0 && 
                entryPoint >= p_vaddr && 
                entryPoint < p_vaddr + p_filesz)
            {
                // 4. Calculate file offset of first instruction
                // file_offset = segment_file_offset + (entry_point - segment_vaddr)
                uint fileOffset = p_offset + (entryPoint - p_vaddr);
            
                // Verify it's within file bounds
                if (fileOffset + 4 <= elfData.Length)
                {
                    return fileOffset;
                }
            }
        }
        throw new InvalidOperationException("Entry point not found in any loadable executable segment");
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

        uint shoff = BitConverter.ToUInt32(elfData, 0x20);
        ushort shentsize = BitConverter.ToUInt16(elfData, 0x2E);
        ushort shnum = BitConverter.ToUInt16(elfData, 0x30);
        ushort shstrndx = BitConverter.ToUInt16(elfData, 0x32); // Section header string table index

        if (shoff == 0 || shnum == 0) return symbols;

        // Read section header string table
        uint shstrHeaderAddr = shoff + (uint)(shstrndx * shentsize);
        uint shstrFileOffset = BitConverter.ToUInt32(elfData, (int)shstrHeaderAddr + 16);
        uint shstrSize = BitConverter.ToUInt32(elfData, (int)shstrHeaderAddr + 20);
        byte[] shstr = new byte[shstrSize];
        Array.Copy(elfData, shstrFileOffset, shstr, 0, shstrSize);

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
            if (sh_type == 2 && sh_entsize > 0) // SHT_SYMTAB
            {
                // Find associated string table
                uint link = BitConverter.ToUInt32(elfData, offset + 24); 
                uint strHeaderAddr = shoff + (link * shentsize);
                uint strOffset = BitConverter.ToUInt32(elfData, (int)strHeaderAddr + 16);
                uint strSize = BitConverter.ToUInt32(elfData, (int)strHeaderAddr + 20);
                byte[] strtab = new byte[strSize];
                Array.Copy(elfData, (int)strOffset, strtab, 0, (int)strSize);
                
                int numSymbols = (int)(sh_size / sh_entsize);
                for (int s = 0; s < numSymbols; s++)
                {
                    int symOffset = (int)(sh_offset + s * sh_entsize);
                    uint st_name = BitConverter.ToUInt32(elfData, symOffset);
                    uint st_value = BitConverter.ToUInt32(elfData, symOffset + 4) + GetFirstExecutableAddress(); // Adjust symbol value to actual memory address
                    

                    string name = ReadString(strtab, st_name);
                    
                    byte st_info = elfData[symOffset + 12];
                    int type = st_info & 0x0F;
                    
                    // Filter out:
                    // type 3 = Section (e.g., .text)
                    // type 4 = File (e.g., bios.s)
                    // name starts with '$' = RISC-V mapping symbols ($x, $d)
                    if (!string.IsNullOrEmpty(name) && type != 3 && type != 4 && !name.StartsWith("$")) 
                    {
                        // Removed the ContainsKey check so real functions overwrite mapping artifacts
                        symbols[st_value] = name;
                    }
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

    public void WriteToMem(Memory memory)
    {
        for (uint i = 0; i < elfData.Length; i++)
        {
            memory.write_byte(i, elfData[i]);
        }
    }
}
