using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using risc_v;

namespace risc_v_GUI.Services;

public class IODevice : IMemoryDevice, IInterruptDevice
{
    public event Action<string>? OutputWritten;
    
    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }
    
    public event Action? interrupt_requested;
    public event Action? interrupt_cleared;
    
    private Queue<char> input_buffer = new Queue<Char>();
    
    public IODevice(uint size, uint start_addr)
    {
        this.size = size;
        this.start_addr = start_addr;
        this.end_addr = start_addr + size;
    }

    public void key_pressed(char key)
    { 
        input_buffer.Enqueue(key);
        interrupt_requested?.Invoke();
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
        if (addr == 8)
        {
            if (input_buffer.Count > 0)
            {
                char key = input_buffer.Dequeue();
                return key;
            }
            return 0;
        }
        throw new Exception("Invalid read address: " + addr);
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
        if (addr == 4 && value == 0)
        {
            interrupt_cleared?.Invoke();
        }
    }
}