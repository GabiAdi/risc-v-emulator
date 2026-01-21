using System.Collections.Generic;
using risc_v;
using SystemHandler = risc_v_GUI.Services.SystemHandler;

namespace risc_v_GUI.Services;

public class EmulatorService
{
    public const string program_path = "../../../assembly/program";
    public ElfLoader loader;
    public Memory memory;
    public Bus bus;
    public Cpu cpu;
    public Disassembler disassembler;
    public SystemHandler system_handler;
    public List<IMemoryDevice> devices;
    
    public EmulatorService()
    {
        loader = new ElfLoader(program_path);
        memory = new Memory(1024 * 1024 * 300, 0x0); // 300 MB
        loader.WriteToMem(memory);
        devices = new List<IMemoryDevice>();
        devices.Add(memory); // 300 MB main memory
        devices.Add(new IODevice(1, 0x20000000));
        bus = new Bus(devices);
        cpu = new Cpu(bus);
        cpu.set_pc(loader.GetFirstExecutableAddress());
        cpu.halt_on_break = true;
        disassembler = new Disassembler(loader.TextStart, loader.GetSymbols());
        system_handler = new SystemHandler(bus, loader.GetFirstExecutableAddress());
        
        cpu.syscall_occured += system_handler.handle_syscall;
        cpu.break_occured += system_handler.handle_breakpoint;
    }
}