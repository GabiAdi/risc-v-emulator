// See https://aka.ms/new-console-template for more information

using risc_v;

Memory memory = new Memory(1024*1024*300);

uint[] program = new uint[]
{
    0x10000517,
    0x00050513,
    0x00400893,
    0x00000073,
    0x00A00893,
    0x00000513,
    0x00000073,

};

memory.write_word(0x10000000, 0x6C6C6548); // Hell
memory.write_word(0x10000004, 0x57202C6F); // o, W
memory.write_word(0x10000008, 0x646C726F); // orld
memory.write_word(0x10000012, 0x210A0000); 

for (uint i = 0; i < program.Length; i++)
{
    memory.write_word(i*4, program[i]);
}

Bus bus = new Bus(memory);
Cpu cpu = new Cpu(bus);

SystemHandler system_handler = new SystemHandler(bus);
cpu.syscall_occured += system_handler.handle_syscall;
cpu.break_occured += system_handler.handle_breakpoint;

while (true)
{
    cpu.step();
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();