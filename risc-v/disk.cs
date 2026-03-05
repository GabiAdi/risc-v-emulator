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
    
    public uint size { get; }
    public uint start_addr { get; }
    public uint end_addr { get; }

    private FileStream disk_file;
    private int file_size;
    private byte[] command_registers;
    private Bus bus;

    public Disk(uint start_addr, uint size=0x18, string file_path="")
    {
        command_registers = new byte[0x18];
        disk_file = new FileStream(file_path, FileMode.Open, FileAccess.ReadWrite);
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
        return (uint)(command_registers[addr] | (command_registers[addr + 1] << 8) | (command_registers[addr + 2] << 16) | (command_registers[addr + 3] << 24));
    }

    public uint read_halfword(uint addr)
    {
        if(addr > 0x16) throw new Exception("Invalid disk register address");
        return (uint)(command_registers[addr] | (command_registers[addr + 1] << 8));
    }
    
    public uint read_byte(uint addr)
    {
        if(addr > 0x17) throw new Exception("Invalid disk register address");
        return (uint)command_registers[addr];
    }
    
    public void write_byte(uint addr, uint value)
    {
        if(addr > 0x17) throw new Exception("Invalid disk register address");
        command_registers[addr] = (byte)(value & 0xFF);
    }
    
    public void write_halfword(uint addr, uint value)
    {
        if(addr > 0x16) throw new Exception("Invalid disk register address");
        command_registers[addr] = (byte)(value & 0xFF);
        command_registers[addr + 1] = (byte)((value >> 8) & 0xFF);
    }
    
    public void write_word(uint addr, uint value)
    {
        if(addr > 0x14) throw new Exception("Invalid disk register address");
        command_registers[addr] = (byte)(value & 0xFF);
        command_registers[addr + 1] = (byte)((value >> 8) & 0xFF);
        command_registers[addr + 2] = (byte)((value >> 16) & 0xFF);
        command_registers[addr + 3] = (byte)((value >> 24) & 0xFF);

        switch (command_registers[(int)command_reg.COMMAND]) 
        {
            case 0: // Idle
                command_registers[(int)command_reg.STATUS] = 0; // Ready
                break;
            case 1: // Read sector
                command_registers[(int)command_reg.STATUS] = 1; // Busy
                
                uint bus_addr = (uint)(command_registers[(int)command_reg.MEM_ADDR_L]);
                foreach (byte data in read((command_registers[(int)command_reg.LBA_LOW])))
                {
                    bus.write(bus_addr, data, 1);
                    bus_addr++;
                }
                
                command_registers[(int)command_reg.STATUS] = 0; // Ready
                break;
            
            case 2: // Write sector
                command_registers[(int)command_reg.STATUS] = 1; // Busy

                byte[] buffer = new byte[512];
                for (int i = 0; i < 512; i++)
                {
                    buffer[i] = (byte)bus.read(command_registers[(int)command_reg.MEM_ADDR_L] + (uint)i, 1);
                }
                write(command_registers[(int)command_reg.LBA_LOW], buffer);
                
                command_registers[(int)command_reg.STATUS] = 0; // Ready
                break;
            
            default:
                throw new Exception("Invalid disk command");
        }
        
    }

    public byte[] read(int sector)
    {
        if(sector * 512 > file_size) throw new Exception("Disk read out of bounds");
        
        byte[] buffer = new byte[512];
        disk_file.ReadExactly(buffer, sector * 512, 512);
        return buffer;
    }
    
    public void write(int sector, byte[] data)
    {
        if(sector * 512 > file_size) throw new Exception("Disk write out of bounds");
        
        disk_file.Write(data, sector*512, 512);
    }
    
    public void clear() {}
}