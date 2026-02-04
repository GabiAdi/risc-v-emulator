namespace risc_v;

public class Bus
{
    private List<IMemoryDevice> devices = new List<IMemoryDevice>();
    public event Action? clear_interrupt;
    public event Action? request_interrupt;

    public Bus(List<IMemoryDevice> devices)
    {
        this.devices = devices;
        
        foreach (IMemoryDevice d1 in devices)
        {
            if (d1 is IInterruptDevice)
            {
                IInterruptDevice interrupt_device = (IInterruptDevice)d1;
                interrupt_device.interrupt_requested += external_interrupt;
                interrupt_device.interrupt_cleared += clear_external_interrupt;
            }
            foreach (IMemoryDevice d2 in devices)
            {
                if(d1 != d2 && d1.start_addr < d2.end_addr && d2.start_addr < d1.end_addr)
                {
                    throw new Exception("Memory devices have overlapping address ranges");
                }
            }
        }
    }

    private void external_interrupt()
    {
        request_interrupt?.Invoke();
    }
    
    private void clear_external_interrupt()
    {
        clear_interrupt?.Invoke();
    }

    private IMemoryDevice get_device(uint addr)
    {
        foreach (IMemoryDevice device in devices)
        {
            if(addr >= device.start_addr && addr < device.end_addr)
            {
                return device;
            }
        }
        throw new Exception("No device");
    }

    public uint read(uint addr, int size)
    {
        IMemoryDevice device = get_device(addr);
        
        if (size == 1)
            return device.read_byte(addr-device.start_addr);
        if (size == 2)
            return device.read_halfword(addr-device.start_addr);
        if (size == 4)
            return device.read_word(addr-device.start_addr);
        throw new Exception($"Bus size {size} not supported");
    }

    public void write(uint addr, uint value, int size)
    {
        IMemoryDevice device = get_device(addr);
        
        if (size == 1)
            device.write_byte(addr-device.start_addr, value);
        else if (size == 2)
            device.write_halfword(addr-device.start_addr, value);
        else if (size == 4)
            device.write_word(addr-device.start_addr, value);
        else
            throw new Exception($"Bus size {size} not supported");
    }
}