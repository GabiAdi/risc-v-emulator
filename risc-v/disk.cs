namespace risc_v;

public class Disk : IMemoryDevice, IDmaDevice
{
    private struct command_regs
    {
        public uint LBA_LOW;
        public uint LBA_HIGH;
        public uint MEM_ADDR_L;
        public uint MEM_ADDR_H;
        public uint COMMAND;
        public uint STATUS;

        public command_regs()
        {
            LBA_LOW = 0;
            LBA_HIGH = 0;
            MEM_ADDR_L = 0;
            MEM_ADDR_H = 0;
            COMMAND = 0;
            STATUS = 0;
        }

        public uint get_reg(uint addr)
        {
            switch (addr)
            {
                case 0x00: return LBA_LOW;
                case 0x04: return LBA_HIGH;
                case 0x08: return MEM_ADDR_L;
                case 0x0C: return MEM_ADDR_H;
                case 0x10: return COMMAND;
                case 0x14: return STATUS;
                default: throw new Exception("Invalid disk register address");
            }
        }

        public void set_reg(uint addr, uint value)
        {
            switch (addr)
            {
                case  0x00: LBA_LOW = value; break;
                case  0x04: LBA_HIGH = value; break;
                case  0x08: MEM_ADDR_L = value; break;
                case  0x0C: MEM_ADDR_H = value; break;
                case  0x10: COMMAND = value; break;
                case  0x14: STATUS = value; break;
                default: throw new Exception("Invalid disk register address");
            }
        }
    }
    
    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }

    private FileStream disk_file;
    private command_regs command_registers;
    private Bus bus;

    public Disk(uint start_addr, uint size=0x14, string file_path="")
    {
        command_registers = new command_regs();
        
        this.size = size;
        this.start_addr = start_addr;
        this.end_addr = start_addr + size;
    }

    public void connect_bus(Bus bus)
    {
        this.bus = bus;
    }

    public uint read_word(uint addr)
    {
        return command_registers.get_reg(addr);
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
        // mem[addr]     = (byte)(value & 0xFF);
        throw new NotImplementedException();
    }
    
    public void write_halfword(uint addr, uint value)
    {
        // mem[addr]     = (byte)(value & 0xFF);
        // mem[addr + 1] = (byte)((value >> 8) & 0xFF);
        throw new NotImplementedException();
    }
    
    public void write_word(uint addr, uint value)
    {
        if(addr > 0x14) throw new Exception("Invalid disk register address");
        command_registers.set_reg(addr, value);
    }
    
    public void clear() {}
}