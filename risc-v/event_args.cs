namespace risc_v;

public class SyscallEventArgs : EventArgs
{
    public uint syscall_number { get; }
    public uint[] args { get; }

    public SyscallEventArgs(uint syscall_number, uint[] args)
    {
        this.syscall_number = syscall_number;
        this.args = args;
    }
}

public class BreakEventArgs : EventArgs
{
    public uint pc { get; }
    public BreakEventArgs(uint pc)
    {
        this.pc = pc;
    }
}