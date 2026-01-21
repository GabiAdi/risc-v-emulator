using System;
using risc_v;

namespace risc_v_GUI.Services;

public class IODevice : IMemoryDevice
{
    public event Action<string>? OutputWritten;
    
    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }
    
    public IODevice(uint size, uint start_addr)
    {
        this.size = size;
        this.start_addr = start_addr;
        this.end_addr = start_addr + size;
    }
    
    public uint read_word(uint addr)
    {
        throw new NotImplementedException();
    }
    
    public uint read_halfword(uint addr)
    {
        throw new NotImplementedException();
    }
    
    public uint read_byte(uint addr) 
    {
        throw new NotImplementedException();
    }
    
    public void write_byte(uint addr, uint value)
    {
        if (addr == 0)
        {
            OutputWritten?.Invoke(((char)(value & 0xFF)) + "");
        }
    }
    
    public void write_halfword(uint addr, uint value)
    {
        if (addr == 0)
        {
            OutputWritten?.Invoke((char)(value & 0xFF) + "" + ((char)(value >> 8) & 0xFF));
        }
    }

    public void write_word(uint addr, uint value)
    {
        if (addr == 0)
        {
            OutputWritten?.Invoke((char)(value & 0xFF) + "" + (char)((value >> 8) & 0xFF) + "" + (char)((value >> 16) & 0xFF) + "" + (char)((value >> 24) & 0xFF));
        }
    }
}