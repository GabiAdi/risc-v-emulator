using System;
using System.Collections.Generic;

public class Disassembler
{
    private static readonly string[] Registers = new string[]
    {
        "x0","x1","x2","x3","x4","x5","x6","x7",
        "x8","x9","x10","x11","x12","x13","x14","x15",
        "x16","x17","x18","x19","x20","x21","x22","x23",
        "x24","x25","x26","x27","x28","x29","x30","x31"
    };

    private readonly Dictionary<uint, string> Symbols;

    // Keep track of last AUIPC to detect 'la'
    private uint? pendingAuipcAddr = null;
    private int pendingAuipcRd = -1;
    private int pendingAuipcImm = 0;

    public Disassembler(Dictionary<uint, string> symbols = null)
    {
        Symbols = symbols ?? new Dictionary<uint, string>();
    }

    private string GetSymbol(uint addr)
    {
        return Symbols.TryGetValue(addr, out var name) ? name : $"0x{addr:X}";
    }

    public string Disassemble(uint instruction, uint pc = 0)
    {
        uint opcode = instruction & 0x7F;
        string decoded = opcode switch
        {
            0x33 => DecodeRType(instruction),      // R-type
            0x03 => DecodeITypeLoad(instruction),  // Loads
            0x13 => DecodeITypeALU(instruction),   // I-type ALU
            0x23 => DecodeSType(instruction),      // Stores
            0x63 => DecodeBType(instruction, pc),  // Branch
            0x37 => DecodeUType(instruction, "lui"),     // LUI
            0x17 => DecodeUType(instruction, "auipc"),   // AUIPC
            0x6F => DecodeJType(instruction, pc),        // JAL
            0x67 => DecodeITypeJALR(instruction),       // JALR
            0x0F => "fence",
            0x73 => DecodeSystem(instruction),
            0x2F => DecodeAtomic(instruction),
            _ => "unknown"
        };

        // Detect pseudo-instructions
        decoded = DetectPseudo(instruction, decoded, pc);

        return decoded;
    }

    // ---------------- R-type ----------------
    private string DecodeRType(uint instr)
    {
        int rd = (int)((instr >> 7) & 0x1F);
        int funct3 = (int)((instr >> 12) & 0x7);
        int rs1 = (int)((instr >> 15) & 0x1F);
        int rs2 = (int)((instr >> 20) & 0x1F);
        int funct7 = (int)((instr >> 25) & 0x7F);

        string mnemonic = funct3 switch
        {
            0x0 => (funct7 == 0x01) ? "mul" : (funct7 == 0x20 ? "sub" : "add"),
            0x1 => (funct7 == 0x01) ? "mulh" : "sll",
            0x2 => "slt",
            0x3 => "sltu",
            0x4 => (funct7 == 0x01) ? "mulhsu" : "xor",
            0x5 => (funct7 == 0x01) ? "mulhu" : (funct7 == 0x20 ? "sra" : "srl"),
            0x6 => "or",
            0x7 => "and",
            _ => "unknown"
        };
        return $"{mnemonic} {Registers[rd]}, {Registers[rs1]}, {Registers[rs2]}";
    }

    // ---------------- I-type ALU ----------------
    private string DecodeITypeALU(uint instr)
    {
        int rd = (int)((instr >> 7) & 0x1F);
        int funct3 = (int)((instr >> 12) & 0x7);
        int rs1 = (int)((instr >> 15) & 0x1F);
        int imm = (int)((instr >> 20) & 0xFFF);
        if ((imm & 0x800) != 0) imm |= unchecked((int)0xFFFFF000);

        string mnemonic = funct3 switch
        {
            0x0 => "addi",
            0x2 => "slti",
            0x3 => "sltiu",
            0x4 => "xori",
            0x6 => "ori",
            0x7 => "andi",
            0x1 => "slli",
            0x5 => ((imm >> 10) & 1) == 1 ? "srai" : "srli",
            _ => "unknown"
        };
        return $"{mnemonic} {Registers[rd]}, {Registers[rs1]}, {imm}";
    }

    // ---------------- I-type Load ----------------
    private string DecodeITypeLoad(uint instr)
    {
        int rd = (int)((instr >> 7) & 0x1F);
        int funct3 = (int)((instr >> 12) & 0x7);
        int rs1 = (int)((instr >> 15) & 0x1F);
        int imm = (int)((instr >> 20) & 0xFFF);
        if ((imm & 0x800) != 0) imm |= unchecked((int)0xFFFFF000);

        string mnemonic = funct3 switch
        {
            0x0 => "lb",
            0x1 => "lh",
            0x2 => "lw",
            0x4 => "lbu",
            0x5 => "lhu",
            _ => "unknown"
        };

        // Try to annotate symbol
        string symbol = GetSymbol((uint)imm);
        return $"{mnemonic} {Registers[rd]}, {imm}({Registers[rs1]}) // {symbol}";
    }

    // ---------------- S-type Store ----------------
    private string DecodeSType(uint instr)
    {
        int imm4_0 = (int)((instr >> 7) & 0x1F);
        int funct3 = (int)((instr >> 12) & 0x7);
        int rs1 = (int)((instr >> 15) & 0x1F);
        int rs2 = (int)((instr >> 20) & 0x1F);
        int imm11_5 = (int)((instr >> 25) & 0x7F);
        int imm = (imm11_5 << 5) | imm4_0;
        if ((imm & 0x800) != 0) imm |= unchecked((int)0xFFFFF000);

        string mnemonic = funct3 switch
        {
            0x0 => "sb",
            0x1 => "sh",
            0x2 => "sw",
            _ => "unknown"
        };

        string symbol = GetSymbol((uint)imm);
        return $"{mnemonic} {Registers[rs2]}, {imm}({Registers[rs1]}) // {symbol}";
    }

    // ---------------- B-type Branch ----------------
    private string DecodeBType(uint instr, uint pc)
    {
        int imm11 = (int)((instr >> 7) & 0x1) << 11;
        int imm4_1 = (int)((instr >> 8) & 0xF) << 1;
        int funct3 = (int)((instr >> 12) & 0x7);
        int rs1 = (int)((instr >> 15) & 0x1F);
        int rs2 = (int)((instr >> 20) & 0x1F);
        int imm10_5 = (int)((instr >> 25) & 0x3F) << 5;
        int imm12 = (int)((instr >> 31) & 0x1) << 12;

        int imm = imm12 | imm11 | imm10_5 | imm4_1;
        if ((imm & 0x1000) != 0) imm |= unchecked((int)0xFFFFE000);

        string mnemonic = funct3 switch
        {
            0x0 => "beq",
            0x1 => "bne",
            0x4 => "blt",
            0x5 => "bge",
            0x6 => "bltu",
            0x7 => "bgeu",
            _ => "unknown"
        };

        uint target = pc + (uint)imm;
        string symbol = GetSymbol(target);
        return $"{mnemonic} {Registers[rs1]}, {Registers[rs2]}, {imm} // {symbol}";
    }

    // ---------------- U-type ----------------
    private string DecodeUType(uint instr, string mnemonic)
    {
        int rd = (int)((instr >> 7) & 0x1F);
        int imm = (int)(instr & 0xFFFFF000);
        return $"{mnemonic} {Registers[rd]}, {imm}";
    }

    // ---------------- J-type ----------------
    private string DecodeJType(uint instr, uint pc)
    {
        int rd = (int)((instr >> 7) & 0x1F);
        int imm20 = (int)((instr >> 31) & 0x1) << 20;
        int imm10_1 = (int)((instr >> 21) & 0x3FF) << 1;
        int imm11 = (int)((instr >> 20) & 0x1) << 11;
        int imm19_12 = (int)((instr >> 12) & 0xFF) << 12;
        int imm = imm20 | imm19_12 | imm11 | imm10_1;
        if ((imm & 0x100000) != 0) imm |= unchecked((int)0xFFE00000);

        uint target = pc + (uint)imm;
        string symbol = GetSymbol(target);
        return $"jal {Registers[rd]}, {imm} // {symbol}";
    }

    // ---------------- I-type JALR ----------------
    private string DecodeITypeJALR(uint instr)
    {
        int rd = (int)((instr >> 7) & 0x1F);
        int rs1 = (int)((instr >> 15) & 0x1F);
        int imm = (int)((instr >> 20) & 0xFFF);
        if ((imm & 0x800) != 0) imm |= unchecked((int)0xFFFFF000);
        return $"jalr {Registers[rd]}, {imm}({Registers[rs1]})";
    }

    // ---------------- System ----------------
    private string DecodeSystem(uint instr)
    {
        int funct3 = (int)((instr >> 12) & 0x7);
        int imm12 = (int)(instr >> 20);
        return funct3 switch
        {
            0x0 => imm12 == 0 ? "ecall" : imm12 == 1 ? "ebreak" : "unknown",
            _ => "csr"
        };
    }

    // ---------------- Atomic ----------------
    private string DecodeAtomic(uint instr)
    {
        int rd = (int)((instr >> 7) & 0x1F);
        int funct3 = (int)((instr >> 12) & 0x7);
        int rs1 = (int)((instr >> 15) & 0x1F);
        int rs2 = (int)((instr >> 20) & 0x1F);
        int funct5 = (int)((instr >> 27) & 0x1F);

        if (funct5 == 0x02 && funct3 == 0x2) return $"lr.w {Registers[rd]}, ({Registers[rs1]})";
        if (funct5 == 0x03 && funct3 == 0x2) return $"sc.w {Registers[rd]}, {Registers[rs2]}, ({Registers[rs1]})";

        string mnemonic = funct5 switch
        {
            0x00 => "amoadd.w",
            0x01 => "amoswap.w",
            0x04 => "amoxor.w",
            0x0C => "amoand.w",
            0x08 => "amoor.w",
            0x10 => "amomin.w",
            0x14 => "amomax.w",
            0x18 => "amominu.w",
            0x1C => "amomaxu.w",
            _ => "unknown"
        };
        return $"{mnemonic} {Registers[rd]}, {Registers[rs2]}, ({Registers[rs1]})";
    }

    // ---------------- Pseudo-instruction detection ----------------
    private string DetectPseudo(uint instr, string decoded, uint pc)
    {
        // nop
        if (instr == 0x00000013) return "nop";

        // mv rd, rs  -> addi rd, rs, 0
        if (decoded.StartsWith("addi") && decoded.EndsWith(", 0"))
            return decoded.Replace("addi", "mv");

        uint opcode = instr & 0x7F;

        // Detect AUIPC for possible 'la'
        if (opcode == 0x17) // AUIPC
        {
            pendingAuipcRd = (int)((instr >> 7) & 0x1F);
            pendingAuipcImm = (int)(instr & 0xFFFFF000);
            pendingAuipcAddr = pc;
            return decoded; // still print AUIPC for now
        }

        // Detect ADDI after AUIPC -> la
        if (opcode == 0x13 && pendingAuipcRd >= 0)
        {
            int rd = (int)((instr >> 7) & 0x1F);
            int rs1 = (int)((instr >> 15) & 0x1F);
            int imm = (int)((instr >> 20) & 0xFFF);
            if ((imm & 0x800) != 0) imm |= unchecked((int)0xFFFFF000); // sign-extend

            if (rd == pendingAuipcRd && rs1 == pendingAuipcRd)
            {
                uint targetAddr = (uint)(pendingAuipcImm + imm);
                string symbol = GetSymbol(targetAddr);

                // Clear pending AUIPC
                pendingAuipcRd = -1;
                pendingAuipcImm = 0;
                pendingAuipcAddr = null;

                return $"la {Registers[rd]}, {symbol}";
            }
        }

        return decoded;
    }
}
