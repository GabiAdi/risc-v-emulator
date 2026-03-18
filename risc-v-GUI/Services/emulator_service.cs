using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
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
    public Dictionary<uint, string> symbols;
    public Bitmap gpu_image;
    
    public EmulatorService()
    {
        devices = new List<IMemoryDevice>();
        byte[] data = new byte[4] {0x6f, 0x10, 0x40, 0x00};
        Rom rom = new Rom(0x0, data);
        devices.Add(rom);
        Ram memory = new Ram(1024 * 1024 * 5, rom.end_addr); // 5 MB
        loader = new ElfLoader(program_path, memory.start_addr);
        loader.WriteToMem(memory);
        devices.Add(memory); // 5 MB main memory
        devices.Add(new IODevice(0xFF000000));
        devices.Add(new Disk(devices.Last().end_addr, "../../../disks/disk.img"));
        devices.Add(new Gpu(devices.Last().end_addr));
        bus = new Bus(devices);
        cpu = new Cpu(bus);
        // cpu.set_pc(loader.GetFirstExecutableAddress());
        cpu.set_pc(0x0);
        cpu.halt_on_break = true;
        symbols = loader.GetSymbols();
        disassembler = new Disassembler(loader.TextStart, symbols);
        system_handler = new SystemHandler(bus, loader.GetFirstExecutableAddress());
        
        cpu.syscall_occured += system_handler.handle_syscall;
        cpu.break_occured += system_handler.handle_breakpoint;
    }
    
    public void load_program(string path)
    {
        ElfLoader old_loader = loader;
        try
        {
            loader = new ElfLoader(path, devices.OfType<Ram>().FirstOrDefault().start_addr);
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
        loader.WriteToMem(bus.devices.OfType<Ram>().FirstOrDefault());
        cpu.set_pc(loader.GetFirstExecutableAddress());
        symbols = loader.GetSymbols();
        disassembler = new Disassembler(loader.TextStart, symbols);
        
        cpu.syscall_occured -= system_handler.handle_syscall;
        cpu.break_occured -= system_handler.handle_breakpoint;
        
        system_handler = new SystemHandler(bus, loader.GetFirstExecutableAddress());
        
        cpu.syscall_occured += system_handler.handle_syscall;
        cpu.break_occured += system_handler.handle_breakpoint;
    }

    public void new_devices(List<IMemoryDevice> devices)
    {
        this.devices = devices;
        bus = new Bus(this.devices);
        load_program(program_path);
    }
}