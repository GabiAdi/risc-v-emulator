using Avalonia.Media.Imaging;

namespace risc_v;

public class Gpu : IMemoryDevice, IDmaDevice
{
    public event Action<Bitmap>? OutputWritten;
    
    enum command_reg
    {
        MEM_ADDR = 0x00,
        MEM_LEN = 0x04,
        COMMAND = 0x08,
        STATUS = 0x0C,
    }

    struct command_regs
    {
        public uint MEM_ADDR;
        public uint MEM_LEN; // In bytes
        public uint COMMAND;
        public uint STATUS;
        
        public command_regs()
        {
            MEM_ADDR = 0;
            MEM_LEN = 0;
            COMMAND = 0;
            STATUS = 0;
        }

        public uint get_reg(uint addr)
        {
            switch (addr) 
            {
                case (uint)command_reg.MEM_ADDR:
                    return MEM_ADDR;
                case (uint)command_reg.MEM_LEN:
                    return MEM_LEN;
                case (uint)command_reg.COMMAND:
                    return COMMAND;
                case (uint)command_reg.STATUS:
                    return STATUS;
                default:
                    throw new Exception($"Unknown gpu reg {addr}");
            }
        }

        public void set_reg(uint addr, uint value)
        {
            switch (addr) 
            {
                case (uint)command_reg.MEM_ADDR:
                    MEM_ADDR = value;
                    break;
                case (uint)command_reg.MEM_LEN:
                    MEM_LEN = value;
                    break;
                case (uint)command_reg.COMMAND:
                    COMMAND = value;
                    break;
                case (uint)command_reg.STATUS:
                    STATUS = value;
                    break;
                default:
                    throw new Exception($"Unknown gpu reg {addr}");
            }
        }
    }

    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }

    private command_regs command_registers;
    public Bitmap image { get; private set; }
    private Bus bus;

    public Gpu(uint start_addr, uint size=16)
    {
        this.start_addr = start_addr;
        this.size = size;
        this.end_addr = start_addr + size;
    }

    public void connect_bus(Bus bus)
    {
        this.bus = bus;
    }

    public uint read_word(uint addr)
    {
        if(addr > size - 4) throw new Exception("Invalid gpu register address"); 
        return command_registers.get_reg(addr);
    }

    public uint read_halfword(uint addr)
    {
        if(addr > size -2) throw new Exception("Invalid gpu register address");
        uint offset = addr - addr % 4;
        return (command_registers.get_reg(offset) >> (int)((addr%4)*8)) & 0xFFFF;
    }
    
    public uint read_byte(uint addr)
    {
        if(addr > size) throw new Exception("Invalid gpu register address");
        uint offset = addr - addr % 4;
        return (command_registers.get_reg(offset) >> (int)((addr%4)*8)) & 0xFF;
    }

    public void write_byte(uint addr, uint value)
    {
        if(addr > size) throw new Exception("Invalid gpu register address");
        uint offset = addr - addr % 4;
        int shift = (int)(addr % 4) * 8;
        uint reg_val = command_registers.get_reg(offset);
        uint mask = ~(0xFFu << shift);
        uint new_val = (reg_val & mask) | ((value & 0xFF) << shift);
        command_registers.set_reg(offset, new_val);
    }

    public void write_halfword(uint addr, uint value)
    {
        if(addr > size - 2) throw new Exception("Invalid gpu register address");
        uint offset = addr - addr % 4;
        int shift = (int)(addr % 4) * 8;
        uint reg_val = command_registers.get_reg(offset);
        uint mask = ~(0xFFFFu << shift);
        uint new_val = (reg_val & mask) | ((value & 0xFFFF) << shift);
        command_registers.set_reg(offset, new_val);
    }

    public void write_word(uint addr, uint value)
    {
        if(addr > size - 4) throw new Exception("Invalid gpu register address");
        command_registers.set_reg(addr, value);

        switch (command_registers.COMMAND)
        {
            case 0: // Idle
                command_registers.STATUS = 0; // Ready
                break;
            case 1: // Read image from memory (any filetype the avalonia bitmap class supports)
                command_registers.STATUS = 1;
                
                byte[] bmp_bytes = new byte[command_registers.MEM_LEN];
                for (uint i = 0; i < command_registers.MEM_LEN; i++)
                {
                    bmp_bytes[i] = (byte)bus.read(command_registers.MEM_ADDR+i, 1);
                }
                
                var stream = new MemoryStream(bmp_bytes);
                image = new Bitmap(stream);
                
                command_registers.STATUS = 0;
                command_registers.COMMAND = 0;
                OutputWritten.Invoke(image);
                break;
        }
    }

    public void clear()
    {
        command_registers = new command_regs();
    }
}