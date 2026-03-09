namespace risc_v;

public class Disk : IMemoryDevice, IDmaDevice
{
    enum command_reg
    {
        LBA_LOW = 0x00,
        LBA_HIGH = 0x04,
        MEM_ADDR_L = 0x08,
        MEM_ADDR_H = 0x0C,
        COMMAND = 0x10,
        STATUS = 0x14,
    }

    struct command_regs
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
                case (uint)command_reg.LBA_LOW:
                    return LBA_LOW;
                case (uint)command_reg.LBA_HIGH:
                    return LBA_HIGH;
                case (uint)command_reg.MEM_ADDR_L:
                    return MEM_ADDR_L;
                case (uint)command_reg.MEM_ADDR_H:
                    return MEM_ADDR_H;
                case (uint)command_reg.COMMAND:
                    return COMMAND;
                case (uint)command_reg.STATUS:
                    return STATUS;
                default:
                    throw new Exception($"Unknown disk reg {addr}");
            }
        }
        
        public void set_reg(uint addr, uint value)
        {
            switch (addr)
            {
                case (uint)command_reg.LBA_LOW:
                    LBA_LOW = value;
                    break;
                case (uint)command_reg.LBA_HIGH:
                    LBA_HIGH = value;
                    break;
                case (uint)command_reg.MEM_ADDR_L:
                    MEM_ADDR_L = value;
                    break;
                case (uint)command_reg.MEM_ADDR_H:
                    MEM_ADDR_H = value;
                    break;
                case (uint)command_reg.COMMAND:
                    COMMAND = value;
                    break;
                case (uint)command_reg.STATUS:
                    STATUS = value;
                    break;
                default:
                    throw new Exception($"Unknown disk reg {addr}");
            }
        }
    }
    
    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }

    private FileStream disk_file;
    private int file_size;
    private command_regs command_registers;
    private Bus bus;

    public Disk(uint start_addr, string file_path, uint size=0x18)
    {
        command_registers = new command_regs();
        disk_file = new FileStream(file_path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        file_size = (int)disk_file.Length;
        
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
        if(addr > 0x14) throw new Exception("Invalid disk register address");   
        return command_registers.get_reg(addr);
    }

    public uint read_halfword(uint addr)
    {
        uint offset = addr - addr % 4;
        return (command_registers.get_reg(offset) >> (int)((addr%4)*8)) & 0xFFFF;
    }
    
    public uint read_byte(uint addr)
    {
        uint offset = addr - addr % 4;
        return (command_registers.get_reg(offset) >> (int)((addr%4)*8)) & 0xFF;
    }
    
    public void write_byte(uint addr, uint value)
    {
        uint offset = addr - addr % 4;
        int shift = (int)(addr % 4) * 8;
        uint reg_val = command_registers.get_reg(offset);
        uint mask = ~(0xFFu << shift);
        uint new_val = (reg_val & mask) | ((value & 0xFF) << shift);
        command_registers.set_reg(offset, new_val);
    }
    
    public void write_halfword(uint addr, uint value)
    {
        uint offset = addr - addr % 4;
        int shift = (int)(addr % 4) * 8;
        uint reg_val = command_registers.get_reg(offset);
        uint mask = ~(0xFFFFu << shift);
        uint new_val = (reg_val & mask) | ((value & 0xFFFF) << shift);
        command_registers.set_reg(offset, new_val);
    }
    
    public void write_word(uint addr, uint value)
    {
        if(addr > 0x14) throw new Exception("Invalid disk register address");

        command_registers.set_reg(addr, value);
        
        switch (command_registers.COMMAND) 
        {
            case 0: // Idle
                command_registers.STATUS = 0; // Ready
                break;
            case 1: // Read sector
                command_registers.STATUS = 1; // Busy
                
                uint bus_addr = command_registers.MEM_ADDR_L;
                foreach (byte data in read(command_registers.LBA_LOW))
                {
                    bus.write(bus_addr, data, 1);
                    bus_addr++;
                }
                
                command_registers.STATUS = 0; // Ready
                command_registers.COMMAND = 0;
                break;
            
            case 2: // Write sector
                command_registers.STATUS = 1; // Busy

                byte[] buffer = new byte[512];
                for (int i = 0; i < 512; i++)
                {
                    buffer[i] = (byte)bus.read(command_registers.MEM_ADDR_L + (uint)i, 1);
                }
                write(command_registers.LBA_LOW, buffer);
                
                command_registers.STATUS = 0; // Ready
                command_registers.COMMAND = 0;
                break;
            
            default:
                throw new Exception("Invalid disk command");
        }
        
    }

    public byte[] read(uint sector)
    {
        if(sector * 512 > file_size) throw new Exception("Disk read out of bounds");
        
        byte[] buffer = new byte[512];
        disk_file.Position = (long)sector * 512;
        disk_file.ReadExactly(buffer, 0, 512);
        return buffer;
    }
    
    public void write(uint sector, byte[] data)
    {
        if(sector * 512 > file_size) throw new Exception("Disk write out of bounds");
        
        disk_file.Position = (long)sector * 512;
        
        disk_file.Write(data, 0, 512);
        disk_file.Flush(true);
    }

    public void clear()
    {
        command_registers = new command_regs();
    }
}