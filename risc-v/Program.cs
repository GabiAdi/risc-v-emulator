using risc_v;
using Reko.Core;
using Reko.Arch.RiscV;
using Reko.Core.Hll.C;
using Reko.Core.Memory;
using System.Collections.Generic;

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

ElfLoader loader = new ElfLoader(program_path);

Dictionary<uint, string> symbols = loader.GetSymbols();

Memory memory = new Memory(1024*1024*300, 0x0); // 300 MB
IODevice io_device = new IODevice(1024, 0x20000000); // I/O device at 0x20000000

loader.WriteToMem(memory);

List<IMemoryDevice> devices = new List<IMemoryDevice>();
devices.Add(memory);
devices.Add(io_device);

Bus bus = new Bus(devices);
Cpu cpu = new Cpu(bus);

cpu.set_pc(loader.GetFirstExecutableAddress());

SystemHandler system_handler = new SystemHandler(bus, loader.GetFirstExecutableAddress());
cpu.syscall_occured += system_handler.handle_syscall;
cpu.break_occured += system_handler.handle_breakpoint;

Disassembler disassembler = new Disassembler(loader.TextStart, symbols);

while (true)
{
    string input = Console.ReadLine();
    if (input == "e" || input == "q") break; // Exit
    if (input == "s") cpu.step();  // Step
    else if (input == "d") // Disassemble
    { 
        Console.WriteLine(disassembler.DisassembleInstruction(cpu.get_current_instruction(), cpu.get_pc()));
    }
    else if (input == "r") // Registers
    {
        for (int i = 0; i < 32; i++)
        {
            Console.WriteLine($"x{i}:\t0x{cpu.get_register(i):X8}");
        }
    }
    else if (input.Split(" ")[0] == "m") // Memory
    {
        uint addr = Convert.ToUInt32(input.Split(" ")[1], 16);
        for (uint i = addr; i < 32; i+=4)
        {
            Console.WriteLine($"0x{i:X8}:\t0x{bus.read(i, 4):X8}");
        }
    }
    else if (input.Split(" ")[0] == "r")
    {
        int reg = Convert.ToInt32(input.Split(" ")[1]);
        Console.WriteLine($"x{reg}:\t0x{cpu.get_register((int)reg):X8}");
    }
}

Console.WriteLine("Press any key to exit...");
Console.ReadKey();