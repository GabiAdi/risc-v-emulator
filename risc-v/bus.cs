namespace risc_v;

public class Bus
{
    private Memory mem;

    public Bus(Memory mem)
    {
        this.mem = mem;
    }

    public uint read(uint addr, int size)
    {
        if (size == 1)
            return mem.read_byte(addr);
        if (size == 2)
            return mem.read_halfword(addr);
        if (size == 4)
            return mem.read_word(addr);
        throw new Exception($"Bus size {size} not supported");
    }

    public void write(uint addr, uint value, int size)
    {
        if (size == 1)
            mem.write_byte(addr, value);
        else if (size == 2)
            mem.write_halfword(addr, value);
        else if (size == 4)
            mem.write_word(addr, value);
        else
            throw new Exception($"Bus size {size} not supported");
    }
}