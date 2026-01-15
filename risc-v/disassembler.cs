using System;
using System.Collections.Generic;
using Reko.Core;
using Reko.Arch.RiscV;
using Reko.Core.Memory;

public class Disassembler
{
    private uint currentAddress;
    private Dictionary<uint, string> symbols;
    private RiscVArchitecture arch;

    public Disassembler(uint startAddress, Dictionary<uint, string> symbols = null)
    {
        currentAddress = startAddress;
        this.symbols = symbols ?? new Dictionary<uint, string>();

        arch = new RiscVArchitecture(
            services: null,
            archId: "rv32ima",
            options: new Dictionary<string, object> { { "isa", "rv32ima" } }
        );
    }

    /// <summary>
    /// Disassembles a single RV32 instruction (4 bytes)
    /// </summary>
    /// <param name="instructionWord">Raw 32-bit instruction</param>
    /// <returns>Disassembled instruction string with symbols</returns>
    public string DisassembleInstruction(uint instructionWord, uint address)
    {
        currentAddress = address;
        byte[] codeBytes = BitConverter.GetBytes(instructionWord);
        var mem = new ByteMemoryArea(Address.Ptr32(currentAddress), codeBytes);
        var reader = mem.CreateLeReader(Address.Ptr32(currentAddress));

        var disasmEnumerable = arch.CreateDisassembler(reader);

        using (var enumerator = disasmEnumerable.GetEnumerator())
        {
            if (!enumerator.MoveNext())
                throw new Exception("Failed to disassemble instruction");

            var ins = enumerator.Current;

            // Split mnemonic and operands
            string[] parts = ins.ToString().Split(new[] { ' ' }, 2);
            string mnemonic = parts[0];
            string operands = parts.Length > 1 ? parts[1] : "";

            // Replace exact addresses in operands only
            foreach (var kvp in symbols)
            {
                string addrHex = $"0x{kvp.Key:X}"; // format like 0xADDR
                operands = operands.Replace(addrHex, kvp.Value);
            }

            // Pad address and mnemonic for alignment
            int addrWidth = 10;
            int mnemonicWidth = 10;

            string text = $"{mnemonic}".PadRight(mnemonicWidth)+
                          $"{operands}";

            // Advance current address
            currentAddress += 4;

            return text;
        }
    }
}
