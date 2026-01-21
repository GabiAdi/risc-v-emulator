namespace risc_v;

public class IODevice : IMemoryDevice
{
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
            Console.Write((char)(value & 0xFF));
        }
    }
    
    public void write_halfword(uint addr, uint value)
    {
        if (addr == 0)
        {
            Console.Write((char)(value & 0xFF) + "" + (char)((value >> 8) & 0xFF));
        }
    }

    public void write_word(uint addr, uint value)
    {
        if (addr == 0)
        {
            Console.Write((char)(value & 0xFF) + "" + (char)((value >> 8) & 0xFF) + "" + (char)((value >> 16) & 0xFF) + "" + (char)((value >> 24) & 0xFF));
        }
    }
}