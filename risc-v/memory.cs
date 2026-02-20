namespace risc_v;

public class Memory : IMemoryDevice
{
    private byte[] mem;
    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }

    public Memory(uint size, uint start_addr)
    {
        mem = new byte[size];
        
        this.size = size;
        this.start_addr = start_addr;
        this.end_addr = start_addr + size;
    }

    public uint read_word(uint addr)
    {
        return (uint)(mem[addr] | (mem[addr + 1] << 8) | (mem[addr + 2] << 16) | (mem[addr + 3] << 24));
    }

    public uint read_halfword(uint addr)
    {
        return (uint)(mem[addr] | (mem[addr + 1] << 8));
    }
    
    public uint read_byte(uint addr)
    {
        return (uint)mem[addr];
    }
    
    public void write_byte(uint addr, uint value)
    {
        mem[addr]     = (byte)(value & 0xFF);
    }
    
    public void write_halfword(uint addr, uint value)
    {
        mem[addr]     = (byte)(value & 0xFF);
        mem[addr + 1] = (byte)((value >> 8) & 0xFF);
    }
    
    public void write_word(uint addr, uint value)
    {
        mem[addr]     = (byte)(value & 0xFF);
        mem[addr + 1] = (byte)((value >> 8) & 0xFF);
        mem[addr + 2] = (byte)((value >> 16) & 0xFF);
        mem[addr + 3] = (byte)((value >> 24) & 0xFF);
    }
    
    public void clear()
    {
        Array.Clear(mem, 0, mem.Length);
    }
}