using risc_v;

uint get_entry_point(string path)
{
    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
    using (BinaryReader br = new BinaryReader(fs))
    {
        // Check ELF magic number: 0x7F 'E' 'L' 'F'
        byte[] magic = br.ReadBytes(4);
        if (magic[0] != 0x7F || magic[1] != 'E' || magic[2] != 'L' || magic[3] != 'F')
            throw new InvalidDataException("Not an ELF file");

        // Skip to entry point (offset 0x18)
        fs.Seek(0x18, SeekOrigin.Begin);
        uint entry = br.ReadUInt32(); // Little-endian (RISC-V)
        return entry;
    }
}

string program_path = "../../../assembly/program";

Memory memory = new Memory(1024*1024*300); // 300 MB

byte[] program_bytes = File.ReadAllBytes(program_path);
for (uint i = 0; i < program_bytes.Length; i++)
{
    memory.write_byte(i, program_bytes[i]);
}

Bus bus = new Bus(memory);
Cpu cpu = new Cpu(bus);

cpu.set_pc(get_entry_point(program_path));

SystemHandler system_handler = new SystemHandler(bus);
cpu.syscall_occured += system_handler.handle_syscall;
cpu.break_occured += system_handler.handle_breakpoint;

while (true)
{
    cpu.step();
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();