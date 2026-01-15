namespace risc_v_GUI.Models;

using System.ComponentModel;
using System.Runtime.CompilerServices;

public class MemoryRow
{
    public string Address { get; set; } = "";
    public string Value { get; set; } = "";
    public string Disassembly { get; set; } = "";
    public string Ascii { get; set; } = "";
    public string Register { get; set; } = "";
}
