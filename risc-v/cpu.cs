using System.Security.AccessControl;

namespace risc_v;

public class Cpu
{
    private struct DecIns // Decoded instruction
    {
        public uint ins;    // Instruction word
        public uint opcode; // [6:0]    Bits 
        public int rd;      // [11:7]   Destination register 
        public int rs1;     // [19:15]  Source register 1 
        public int rs2;     // [24:20]  Source register 2 
        public int funct3;  // [14:12]
        public int funct7;  // [32:25]
        public uint imm;    // Immediate value
        
        // Register values
        public uint val_rs1;
        public uint val_rs2;    

        public DecIns(uint instruction)
        { 
            ins     = instruction;
            opcode  = instruction & 0x7F;
            rd      = (int)((instruction >> 7) & 0x1F);
            funct3  = (int)((instruction >> 12) & 0x7);
            rs1     = (int)((instruction >> 15) & 0x1F);
            rs2     = (int)((instruction >> 20) & 0x1F);
            funct7  = (int)((instruction >> 25) & 0x7F);
            imm     = 0;
            val_rs1 = 0;
            val_rs2 = 0;
        }
    }
    
    private struct ExecResult
    {
        // Register write
        public int dest;         // Destination register number (0 = none)
        public uint result;      // Value to write to that register

        // Memory write
        public bool write_to_memory; 
        public bool read_from_memory;
        public bool zero_extend;
        public uint mem_addr;
        public uint mem_write_val;
        public int mem_width;     // 1=byte, 2=half, 4=word (optional)

        // Control flow
        public bool branch_taken;
        public uint new_pc;

        public ExecResult()
        {
            dest = 0;
            result = 0;
            write_to_memory = false;
            read_from_memory = false;
            zero_extend = false;
            mem_addr = 0;
            mem_write_val = 0;
            mem_width = 0;
            branch_taken = false;
            new_pc = 0;
        }
    }
    
    private uint[] reg = new uint[32]; // 32 General registers
    private uint pc; // Program counter

    private Bus bus; // Bus for reading and writing to memory

    public event EventHandler<SyscallEventArgs> syscall_occured;
    public event EventHandler<BreakEventArgs> break_occured;

    private void on_syscall(uint syscall_number, uint[] args)
    {
        syscall_occured?.Invoke(this, new SyscallEventArgs(syscall_number, args));
    }

    private void on_break(uint pc)
    {
        break_occured?.Invoke(this, new BreakEventArgs(pc));
    }
    
    private uint get_reg(int index)
    {
        return index == 0 ? 0u : reg[index];
    }

    private void set_reg(int index, uint value)
    {
        reg[index] = value;
    }

    private uint dec_imm(uint instr)
    {
        uint opcode  = instr & 0x7F;
        int imm = 0;
        
        switch (opcode)
        {
            case 0x13: // I-type (ADDI, ANDI, ORI, etc.)
            case 0x03: // Loads
            case 0x67: // JALR
            case 0x73:
                imm = (int)instr >> 20; // bits [31:20] -> sign-extended
                break;

            case 0x23: // S-type (stores)
                int imm4_0 = (int)((instr >> 7) & 0x1F);    // bits [11:7]
                int imm11_5 = (int)((instr >> 25) & 0x7F);  // bits [31:25]
                imm = (imm11_5 << 5) | imm4_0;
                // Sign-extend
                if ((imm & 0x800) != 0) imm |= unchecked((int)0xFFFFF000);
                break;

            case 0x63: // B-type (branches)
                int imm11 = (int)((instr >> 7) & 0x1) << 11;
                int imm4_1 = (int)((instr >> 8) & 0xF) << 1;
                int imm10_5 = (int)((instr >> 25) & 0x3F) << 5;
                int imm12 = (int)((instr >> 31) & 0x1) << 12;
                imm = imm12 | imm11 | imm10_5 | imm4_1;
                // Sign-extend 13-bit immediate
                if ((imm & 0x1000) != 0) imm |= unchecked((int)0xFFFFE000);
                break;

            case 0x37: // LUI
            case 0x17: // AUIPC
                imm = (int)(instr & 0xFFFFF000); // upper 20 bits
                break;

            case 0x6F: // J-type (JAL)
                int imm20 = (int)((instr >> 31) & 0x1) << 20;
                int imm10_1 = (int)((instr >> 21) & 0x3FF) << 1;
                int imm11_j = (int)((instr >> 20) & 0x1) << 11;
                int imm19_12 = (int)((instr >> 12) & 0xFF) << 12;
                imm = imm20 | imm19_12 | imm11_j | imm10_1;
                // Sign-extend 21-bit
                if ((imm & 0x100000) != 0) imm |= unchecked((int)0xFFE00000);
                break;
        }

        
        return (uint)imm;
    }

    private ExecResult execute(DecIns d, uint pc)
    {
        ExecResult r = new ExecResult();

        switch (d.opcode)
        {
            case 0x3: // I type
                if (d.funct3 == 0x0) // LB 
                {
                    r.mem_addr = d.val_rs1 + d.imm;
                    r.mem_width = 1;
                    r.dest = d.rd;
                    r.read_from_memory = true;
                }
                else if (d.funct3 == 0x1) // LH
                {
                    r.mem_addr = d.val_rs1 + d.imm;
                    r.mem_width = 2;
                    r.dest = d.rd;
                    r.read_from_memory = true;
                }
                else if (d.funct3 == 0x2) // LW
                {
                    r.mem_addr = d.val_rs1 + d.imm;
                    r.mem_width = 4;
                    r.dest = d.rd;
                    r.read_from_memory = true;
                }
                else if (d.funct3 == 0x4) // LBU
                {
                    r.mem_addr = d.val_rs1 + d.imm;
                    r.mem_width = 1;
                    r.dest = d.rd;
                    r.read_from_memory = true;
                    r.zero_extend = true; 
                }
                else if (d.funct3 == 0x5) // LHU
                {
                    r.mem_addr = d.val_rs1 + d.imm;
                    r.mem_width = 1;
                    r.dest = d.rd;
                    r.read_from_memory = true;
                    r.zero_extend = true; 
                }
                break;
            case 0x13: // I type
                if (d.funct3 == 0x0) // ADDI
                {
                    r.result = d.val_rs1 + d.imm;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x1 && d.funct7 == 0x00) // SLLI
                {
                    uint shamt = d.imm & 0x1F;
                    r.result = d.val_rs1 << (int)shamt;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x5 && d.funct7 == 0x00) // SRLI
                {
                    uint shamt = d.imm & 0x1F;
                    r.result = d.val_rs1 >> (int)shamt;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x5 && d.funct7 == 0x20) // SRAI
                {
                    uint shamt = d.imm & 0x1F;
                    r.result = (uint)((int)d.val_rs1 >> (int)shamt);
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x2) // SLTI
                {
                    r.result = (int)d.val_rs1 < (int)d.imm ? (uint)1 : (uint)0;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x3) // SLTIU
                {
                    r.result = d.val_rs1 < d.imm ? (uint)1 : (uint)0;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x4) // XORI
                {
                    r.result = d.val_rs1 ^ d.imm;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x6) // ORI
                {
                    r.result = d.val_rs1 | d.imm;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x7) // ANDI
                {
                    r.result = d.val_rs1 & d.imm;
                    r.dest = d.rd;
                }
                break;
            
            case 0x17: // AUIPC
                r.result = pc + d.imm;
                r.dest = d.rd;
                break;
            
            case 0x23: // S type
                if (d.funct3 == 0x0) // SB
                {
                    r.mem_addr = d.val_rs1 + d.imm;
                    r.mem_write_val = d.val_rs2 & 0xFF;
                    r.mem_width = 1;
                    r.write_to_memory = true;
                }
                else if (d.funct3 == 0x1) // SH
                {
                    r.mem_addr = d.val_rs1 + d.imm;
                    r.mem_write_val = d.val_rs2 & 0xFFFF;
                    r.mem_width = 2;
                    r.write_to_memory = true;
                }
                else if (d.funct3 == 0x2) // SW
                {
                    r.mem_addr = d.val_rs1 + (uint)d.imm;
                    r.mem_write_val = d.val_rs2;
                    r.mem_width = 4;
                    r.write_to_memory = true;
                }
                break;
            
            case 0x33: // R type
                if (d.funct3 == 0x0 && d.funct7 == 0x0) // ADD
                {
                    r.result = d.val_rs1 + d.val_rs2;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x0 && d.funct7 == 0x20) // SUB
                {
                    r.result = d.val_rs1 - d.val_rs2;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x1 && d.funct7 == 0x00) // SLL
                {
                    r.result = (uint)((int)d.val_rs1 << (int)(d.val_rs2 & 0x1F));
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x5 && d.funct7 == 0x00) // SRL
                {
                    r.result = d.val_rs1 >> (int)(d.val_rs2 & 0x1F);
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x5 && d.funct7 == 0x20) // SRA
                {
                    r.result = (uint)((int)d.val_rs1 >> (int)(d.val_rs2  & 0x1F));
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x2 && d.funct7 == 0x00) // SLT
                {
                    r.result = (int)d.val_rs1 < (int)d.val_rs2 ? (uint)1 : (uint)0; 
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x3 && d.funct7 == 0x00) // SLTU
                {
                    r.result = d.val_rs1 < d.val_rs2 ? (uint)1 : (uint)0;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x7 && d.funct7 == 0x0) // AND
                {
                    r.result = d.val_rs1 & d.val_rs2;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x6 && d.funct7 == 0x0) // OR 
                {
                    r.result = d.val_rs1 | d.val_rs2;
                    r.dest = d.rd;
                }
                else if (d.funct3 == 0x4 && d.funct7 == 0x00) // XOR
                {
                    r.result = d.val_rs1 ^ d.val_rs2;
                    r.dest = d.rd;
                }
                break;
            
            case 0x37: // LUI
                r.result = d.imm;
                r.dest = d.rd;
                break;
            
            case 0x63: // B type
                if (d.funct3 == 0x0) // BEQ
                {
                    r.branch_taken = d.val_rs1 == d.val_rs2;
                    r.new_pc = pc + d.imm;
                }
                else if (d.funct3 == 0x1) // BNE
                {
                    r.branch_taken = d.val_rs1 != d.val_rs2;
                    r.new_pc = pc + d.imm;
                }
                else if (d.funct3 == 0x4) // BLT
                {
                    r.branch_taken = (int)d.val_rs1 < (int)d.val_rs2;
                    r.new_pc = pc + d.imm;
                }
                else if (d.funct3 == 0x5) // BGE
                {
                    r.branch_taken = (int)d.val_rs1 >= (int)d.val_rs2;
                    r.new_pc = pc + d.imm;
                }
                else if (d.funct3 == 0x6) // BLTU
                {
                    r.branch_taken = d.val_rs1 < d.val_rs2;
                    r.new_pc = pc + d.imm;
                }
                else if (d.funct3 == 0x7) // BGEU
                {
                    r.branch_taken = d.val_rs1 >= d.val_rs2;
                    r.new_pc = pc + d.imm;
                }
                break;
            
            case 0x67: // JALR
                r.branch_taken = true;
                r.new_pc = (d.val_rs1 + d.imm) & 0xFFFFFFFE;
                r.result = pc + 4;
                r.dest = d.rd;
                break;
            
            case 0x6F: // JAL
                r.branch_taken = true;
                r.new_pc = pc + d.imm;
                r.result = pc + 4;
                r.dest = d.rd;
                break;
            
            case 0x73: // SYSTEM
                if (d.imm == 0) // ECALL
                {
                    uint syscall = get_register(17);
                    uint[] args = new uint[7];
                    Array.Copy(reg, 10, args, 0, 7);
                    on_syscall(syscall, args);
                }
                else if (d.imm == 1) // EBREAK
                {
                    on_break(pc);
                }
                break;
                
        }
        return r;
    }

    private uint sign_extend(uint value, int width)
    {
        switch (width)
        {
            case 1: // LB
                return (value & 0x80) != 0 ? value | 0xFFFFFF00U : value;
            case 2: // LH
                return (value & 0x8000) != 0 ? value | 0xFFFF0000U : value;
        }
        return value; // LW
    }

    private void write_back(ExecResult r)
    {
        if (r.read_from_memory)
        {
            if (r.zero_extend)
                r.result = bus.read(r.mem_addr, r.mem_width);
            else
                r.result = sign_extend(bus.read(r.mem_addr, r.mem_width), r.mem_width);
        }
        if (r.write_to_memory) bus.write(r.mem_addr, r.mem_write_val, r.mem_width);
        if (r.dest != 0) set_reg(r.dest, r.result); // Cannot write to 0x0
        if (r.branch_taken) pc = r.new_pc;
    }

    private DecIns decode(uint instr)
    {
        DecIns decoded = new DecIns(instr);
        decoded.imm = dec_imm(decoded.ins);
        decoded.val_rs1 = get_reg(decoded.rs1);
        decoded.val_rs2 = get_reg(decoded.rs2);
        return decoded;
    }
    
    public Cpu(Bus bus)
    {
        this.bus = bus;
        
        reg[0] = 0; // Register 0x0 is always zero and cannot be written to
        pc = 0;
    }

    public uint get_register(int index)
    {
        if (index == 0) return 0; // Register 0x0 is always zero
        if (index > reg.Length) throw new IndexOutOfRangeException();
        return reg[index];
    }

    public void set_register(int index, uint value)
    {
        if (index == 0) return; // Cannot write to register 0x0
        if (index > reg.Length) throw new IndexOutOfRangeException();
        reg[index] = value;
    }

    public void step()
    {
        uint ins = bus.read(pc, 4); // Fetch
        uint current_pc = pc;
        pc += 4; // Increment PC by 4 bytes
        
        var decoded = decode(ins);
        var exec = execute(decoded, current_pc);
        write_back(exec);
    }
}