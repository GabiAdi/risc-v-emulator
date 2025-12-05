using risc_v;

byte[] uint_to_bytes(uint value)
{
    return new byte[4]
    {
        (byte)(value & 0xFF),
        (byte)((value >> 8) & 0xFF),
        (byte)((value >> 16) & 0xFF),
        (byte)((value >> 24) & 0xFF)
    };
}

void WriteHex(uint[] data)
{
    if (data == null || data.Length == 0)
    {
        Console.WriteLine("No data to write.");
        return;
    }

    foreach (var word in data)
    {
        Console.WriteLine($"0x{word:X8}");
    }
}

string program_path = "../../../assembly/program";

Memory memory = new Memory(1024*1024*300); // 300 MB

ElfLoader loader = new ElfLoader(program_path);

uint[] program_bytes = loader.GetExecutableWords();

for (uint i = 0; i < program_bytes.Length; i+=4)
{
    memory.write_word(i, program_bytes[i]);
}

Bus bus = new Bus(memory);
Cpu cpu = new Cpu(bus);

cpu.set_pc(loader.EntryPoint);

Console.WriteLine($"Entry Point: 0x{loader.EntryPoint:X}");
Console.WriteLine($"Execstart: " + loader.TextStart);
Console.WriteLine("Execend: " + loader.TextEnd);

SystemHandler system_handler = new SystemHandler(bus);
cpu.syscall_occured += system_handler.handle_syscall;
cpu.break_occured += system_handler.handle_breakpoint;

Disassembler disassembler = new Disassembler(loader.GetSymbols());

while (true)
{
    cpu.step();
    Console.Write(cpu.get_pc() + ": ");
    Console.WriteLine(disassembler.Disassemble(cpu.get_current_instruction()));
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();