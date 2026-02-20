namespace risc_v;

public interface IMemoryDevice
{
    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }
    
    public uint read_word(uint addr);
    public uint read_halfword(uint addr);
    public uint read_byte(uint addr);
    public void write_byte(uint addr, uint value);
    public void write_halfword(uint addr, uint value);
    public void write_word(uint addr, uint value);
    public void clear();
}