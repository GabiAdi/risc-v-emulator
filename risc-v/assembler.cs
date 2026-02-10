using System.Diagnostics;
using System.Runtime.InteropServices;

namespace risc_v;

public class Assembler
{
    public static void assemble(string source, string out_file, string directory, string linker_file = "")
    {
        string tool_prefix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"{directory}tools/win/bin/riscv-none-elf-"
            : $"{directory}tools/linux/bin/riscv-none-elf-";
        string assembler = tool_prefix+"as" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "");
        string linker = tool_prefix+"ld" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "");

        run_process(assembler, $"-march=rv32imazicsr -o {directory}/assembly/program.o {directory}/assembly/{source}");
        run_process(assembler, $"-march=rv32imazicsr -o {directory}/assembly/bios.o {directory}/assembly/bios.s");
        run_process(linker, linker_file != "" ? $"-T {directory}/assembly/{linker_file} -o {directory}/assembly/{out_file} {directory}/assembly/program.o {directory}/assembly/bios.o" : $"-o {directory}/assembly/{out_file} {directory}/assembly/program.o {directory}/assembly/bios.o");
    }

    private static void run_process(string program, string args)
    {
        var start_info = new ProcessStartInfo
        {
            FileName = program,
            Arguments = args,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        
        using var process = Process.Start(start_info);
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string error = process.StandardError.ReadToEnd();
            throw new Exception($"Process {program} exited with code {process.ExitCode}: {error}");
        }
    }
}