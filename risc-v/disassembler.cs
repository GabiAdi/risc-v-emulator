using System;
using System.Collections.Generic;
using Reko.Core;
using Reko.Arch.RiscV;
using Reko.Core.Memory;
using Reko.Core.Machine;

public class Disassembler
{
    private uint currentAddress;
    private Dictionary<uint, string> symbols;
    private RiscVArchitecture arch;

    public Disassembler(uint startAddress, Dictionary<uint, string> rawSymbols)
    {
        currentAddress = startAddress;

        // Convert to Reko Address-keyed dictionary for native symbol resolution
        symbols = rawSymbols;

        arch = new RiscVArchitecture(
            services: null,
            archId: "rv32ima",
            options: new Dictionary<string, object> { { "isa", "rv32ima" } }
        );
    }

    public string DisassembleInstruction(uint instructionWord, uint address)
    {
        currentAddress = address;
        byte[] codeBytes = BitConverter.GetBytes(instructionWord);
        var mem = new ByteMemoryArea(Address.Ptr32(currentAddress), codeBytes);
        var reader = mem.CreateLeReader(Address.Ptr32(currentAddress));

        var disasmEnumerable = arch.CreateDisassembler(reader);

        using (var enumerator = disasmEnumerable.GetEnumerator())
        {
            try
            {
                if (!enumerator.MoveNext())
                    throw new Exception("Failed to disassemble instruction");
            }
            catch (NullReferenceException)
            {
                return "invalid";
            }

            var ins = enumerator.Current;

            var options = new MachineInstructionRendererOptions(
                flags: MachineInstructionRendererFlags.None
            );

            var renderer = new StringRenderer();
            ins.Render(renderer, options);
            string rendered = renderer.ToString();

            // Replace addresses in Reko's exact output format (zero-padded 8 digit hex)
            foreach (var kvp in symbols)
            {
                string rekoAddr = $"0x{kvp.Key:X8}";
                rendered = rendered.Replace(rekoAddr, kvp.Value, StringComparison.OrdinalIgnoreCase);
            }

            string[] parts = rendered.Split(new[] { '\t', ' ' }, 2);
            string mnemonic = parts[0];
            string operands = parts.Length > 1 ? parts[1] : "";

            string text = mnemonic.PadRight(10) + operands;

            currentAddress += (uint)ins.Length;  // use actual instruction length (handles compressed 16-bit RVC too)

            return text;
        }
    }
}