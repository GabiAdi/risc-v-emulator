// See https://aka.ms/new-console-template for more information

using risc_v;

Memory memory = new Memory(1024*1024*5);

uint[] program = new uint[]
{
    0x00001537,
    0x01200093,
    0x00209113,
    0x00115193,
    0x4011D213,
    0x00452023,
    0x00054283,
    0x00055303,
    0x00000393,
    0x00138393,
    0xFE53EEE3,
    0x00100073,
};

for (uint i = 0; i < program.Length; i++)
{
    memory.write_word(i*4, program[i]);
}

Bus bus = new Bus(memory);
Cpu cpu = new Cpu(bus);

// SystemHandler system_handler = new SystemHandler(bus);
// cpu.break_occured += system_handler.handle_breakpoint;

// for(int i=0; i<32; i++) 
// {
//     cpu.step();
// }

while (true)
{
    cpu.step();
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();