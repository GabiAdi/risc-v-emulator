using System;
using System.Text;
using risc_v;

namespace risc_v_GUI.Services;

public class SystemHandler
{
    public event Action<string>? OutputProduced;
    public event Action<string>? StatusChanged;
    
    private Bus bus;
    private uint word_start;
    
    public SystemHandler(Bus bus, uint word_start)
    {
        this.bus = bus;
        this.word_start = word_start;
    }

    public void handle_syscall(object sender, SyscallEventArgs e)
    {
        switch (e.syscall_number) 
        {
            case 1: // print int
                OutputProduced?.Invoke(e.args[0].ToString());
                break;
            case 4: // print string
                uint addr = e.args[0] - word_start;
                StringBuilder sb = new StringBuilder();
                uint b;
                while ((b = bus.read(addr++, 1)) != 0)
                {
                    sb.Append((char)b);
                }
                OutputProduced?.Invoke(sb.ToString());
                break;
            case 11: // print char
                OutputProduced?.Invoke(((char)e.args[0]).ToString());
                break;
            case 10:
            case 93:
                StatusChanged?.Invoke("Program exited with code " + e.args[0]);
                break;
        }
    }

    public void handle_breakpoint(object sender, BreakEventArgs e)
    {
        StatusChanged?.Invoke($"Breakpoint hit at PC=0x{e.pc:X}");
    }
}