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
    
    public EmulatorService()
    {
        loader = new ElfLoader(program_path);
        memory = new Memory(1024 * 1024 * 300); // 300 MB
        loader.WriteToMem(memory);
        bus = new Bus(memory);
        cpu = new Cpu(bus);
        cpu.set_pc(loader.GetFirstExecutableAddress());
        disassembler = new Disassembler(loader.TextStart, loader.GetSymbols());
        system_handler = new SystemHandler(bus, loader.GetFirstExecutableAddress());
        
        cpu.syscall_occured += system_handler.handle_syscall;
        cpu.break_occured += system_handler.handle_breakpoint;
    }
}