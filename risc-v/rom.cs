namespace risc_v;

public class Rom : IMemoryDevice
{
    private readonly byte[] mem;
    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }
    public string file_path { get; }

    public Rom(uint start_addr, string file_path)
    {
        mem = File.ReadAllBytes(file_path);
        
        this.size = (uint)mem.Length;
        this.start_addr = start_addr;
        this.end_addr = start_addr + size;
        this.file_path = file_path;
    }

    public Rom(uint start_addr, byte[] data)
    {
        mem = data;
        
        this.size = (uint)mem.Length;
        this.start_addr = start_addr;
        this.end_addr = start_addr + size;
        file_path = "";
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
        return;
    }
    
    public void write_halfword(uint addr, uint value)
    {
        return;
    }
    
    public void write_word(uint addr, uint value)
    {
        return;
    }
    
    public void clear()
    {
        return;
    }
}