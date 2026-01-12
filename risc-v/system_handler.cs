using System.Text;

namespace risc_v;

public class SystemHandler
{
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
                Console.Write(e.args[0]);
                break;
            case 4: // print string
                uint addr = e.args[0] - word_start;
                StringBuilder sb = new StringBuilder();
                uint b;
                while ((b = bus.read(addr++, 1)) != 0)
                {
                    sb.Append((char)b);
                }
                Console.WriteLine(sb.ToString());
                break;
            case 11:
                Console.Write((char)e.args[0]);
                break;
            case 10:
            case 93:
                Console.Write($"Program exited with code {e.args[0]}");
                Environment.Exit((int)e.args[0]);
                break;
        }
    }

    public void handle_breakpoint(object sender, BreakEventArgs e)
    {
        Console.WriteLine($"Breakpoint hit at PC=0x{e.pc:X}");
    }
}