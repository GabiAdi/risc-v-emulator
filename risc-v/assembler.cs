using System.Diagnostics;
using System.Runtime.InteropServices;

namespace risc_v;

public static class Assembler
{
    private static readonly string root_directory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
    
    // Build paths relative to the resolved RootDirectory
    private static readonly string bios_file = Path.Combine(root_directory, "assembly", "bios.s");
    private static readonly string assembly_folder = Path.Combine(root_directory, "assembly");

    public static void assemble(string source_file_path, string out_file_path, string linker_file = "")
    {
        string os_folder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "linux";
        string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";

        string toolsBin = Path.Combine(root_directory, "tools", os_folder, "bin");
        string assembler = Path.Combine(toolsBin, $"riscv-none-elf-as{extension}");
        string linker = Path.Combine(toolsBin, $"riscv-none-elf-ld{extension}");

        string program_obj = Path.Combine(assembly_folder, "program.o");
        string bios_obj = Path.Combine(assembly_folder, "bios.o");

        run_process(assembler, $"-I \"{assembly_folder}\" -march=rv32imazicsr -o \"{program_obj}\" \"{source_file_path}\"", root_directory);

        run_process(assembler, $"-I \"{assembly_folder}\" -march=rv32imazicsr -o \"{bios_obj}\" \"{bios_file}\"", root_directory);

        string linkerArgs = linker_file != "" 
            ? $"-T \"{Path.Combine(assembly_folder, linker_file)}\" -o \"{out_file_path}\" \"{program_obj}\" \"{bios_obj}\"" 
            : $"-o \"{out_file_path}\" \"{program_obj}\" \"{bios_obj}\"";

        run_process(linker, linkerArgs, root_directory);
    }

    private static void run_process(string program, string args, string root_directory)
    {
        var start_info = new ProcessStartInfo
        {
            FileName = program,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = root_directory 
        };
    
        using var process = Process.Start(start_info);

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            string error = process.StandardError.ReadToEnd();
            throw new Exception($"Process {Path.GetFileName(program)} failed (Code {process.ExitCode}): {error}");
        }
    }
}