using System;
using System.Collections.Generic;
using System.Linq;
using risc_v;
using SystemHandler = risc_v_GUI.Services.SystemHandler;

namespace risc_v_GUI.Services;

public class EmulatorService
{
    public const string program_path = "../../../assembly/program.elf";
    public ElfLoader loader;
    public Bus bus;
    public Cpu cpu;
    public Disassembler disassembler;
    public SystemHandler system_handler;
    public List<IMemoryDevice> devices;
    
    public EmulatorService()
    {
        loader = new ElfLoader(program_path);
        Memory memory = new Memory(1024 * 1024 * 5, 0x0); // 5 MB
        loader.WriteToMem(memory);
        devices = new List<IMemoryDevice>();
        devices.Add(memory); // 5 MB main memory
        devices.Add(new IODevice(12, 1024 * 1024 * 5));
        bus = new Bus(devices);
        cpu = new Cpu(bus);
        cpu.set_pc(loader.GetFirstExecutableAddress());
        cpu.halt_on_break = true;
        disassembler = new Disassembler(loader.TextStart, loader.GetSymbols());
        system_handler = new SystemHandler(bus, loader.GetFirstExecutableAddress());
        
        cpu.syscall_occured += system_handler.handle_syscall;
        cpu.break_occured += system_handler.handle_breakpoint;
    }

    public void load_program(string path)
    {
        ElfLoader old_loader = loader;
        try
        {
            loader = new ElfLoader(path);
        }
        catch (Exception e)
        {
            loader = old_loader;
            throw e;
        }
        
        foreach (IMemoryDevice device in devices)
        {
            device.clear();
        }
        loader.WriteToMem(bus.devices.OfType<Memory>().FirstOrDefault());
        cpu.set_pc(loader.GetFirstExecutableAddress());
        disassembler = new Disassembler(loader.TextStart, loader.GetSymbols());
        
        cpu.syscall_occured -= system_handler.handle_syscall;
        cpu.break_occured -= system_handler.handle_breakpoint;
        
        system_handler = new SystemHandler(bus, loader.GetFirstExecutableAddress());
        
        cpu.syscall_occured += system_handler.handle_syscall;
        cpu.break_occured += system_handler.handle_breakpoint;
    }
}