namespace risc_v;

public class IODevice : IMemoryDevice, IInterruptDevice
{
    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }
    
    public event Action? interrupt_requested;
    public event Action? interrupt_cleared;
    
    private char[] buffer = new char[4] { '\0', '\0', '\0', '\0' };
    
    public IODevice(uint size, uint start_addr)
    {
        this.size = size;
        this.start_addr = start_addr;
        this.end_addr = start_addr + size;
    }
    
    public uint read_word(uint addr)
    {
        if(addr > 0) throw new ArgumentOutOfRangeException(nameof(addr), "Address out of range for IODevice");
        return (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24));
    }
    
    public uint read_halfword(uint addr)
    {
        if(addr > 2) throw new ArgumentOutOfRangeException(nameof(addr), "Address out of range for IODevice");
        return (uint)(buffer[addr] | (buffer[addr+1] << 8));
    }
    
    public uint read_byte(uint addr) 
    {
        if(addr > 4) throw new ArgumentOutOfRangeException(nameof(addr), "Address out of range for IODevice");
        return (uint)buffer[addr];
    }
    
    public void write_byte(uint addr, uint value)
    {
        if (addr == 0)
        {
            buffer[0] = (char)(value & 0xFF);
            Console.Write((char)(value & 0xFF));
        }
    }
    
    public void write_halfword(uint addr, uint value)
    {
        if (addr == 0)
        {
            buffer[0] = (char)(value & 0xFF);
            buffer[1] = (char)((value >> 8) & 0xFF);
            Console.Write((char)(value & 0xFF) + "" + (char)((value >> 8) & 0xFF));
        }
    }

    public void write_word(uint addr, uint value)
    {
        if (addr == 0)
        {
            buffer[0] = (char)(value & 0xFF);
            buffer[1] = (char)((value >> 8) & 0xFF);
            buffer[2] = (char)((value >> 16) & 0xFF);
            buffer[3] = (char)((value >> 24) & 0xFF);
            Console.Write((char)(value & 0xFF) + "" + (char)((value >> 8) & 0xFF) + "" + (char)((value >> 16) & 0xFF) + "" + (char)((value >> 24) & 0xFF));
        }
        if (addr == 4 && value == 0)
        {
            interrupt_cleared?.Invoke();
        }
    }

    public void clear()
    {

        Array.Clear(buffer, 0, buffer.Length);
        interrupt_cleared?.Invoke();
    }
}