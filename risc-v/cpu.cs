using System.Security.AccessControl;

namespace risc_v;

public class Cpu
{
    private const ushort CSR_MSTATUS  = 0x300;
    private const ushort CSR_MIE      = 0x304;
    private const ushort CSR_MTVEC    = 0x305;
    private const ushort CSR_MSCRATCH = 0x340;
    private const ushort CSR_MEPC     = 0x341;
    private const ushort CSR_MCAUSE   = 0x342;
    private const ushort CSR_MTVAL    = 0x343;
    private const ushort CSR_MIP      = 0x344;
    
    private const uint MSTATUS_MIE  = 1u << 3;
    private const uint MSTATUS_MPIE = 1u << 7;

    private const uint MIE_MSIE = 1u << 3;
    private const uint MIE_MTIE = 1u << 7;
    private const uint MIE_MEIE = 1u << 11;
    
    private enum OpType
    {
        LR,
        SC,
        AMO,
        CSR,
        INTERRUPT,
        OTHER,
    }
    
    private struct CsrIns
    {
        public int instruction;
        public uint val_rs1;
        public uint csr;
        public int rd;
        public uint zimm;
        
        public CsrIns(int instruction, uint val_rs1, uint csr, int rd, uint zimm)
        {
            this.instruction = instruction;
            this.val_rs1 = val_rs1;
            this.csr = csr;
            this.rd = rd;
            this.zimm = zimm;
        }
    }
    
    private struct AtomicIns
    {
        public int instruction;
        public uint old;
        public uint rs2;
        
        public AtomicIns(int instruction, uint old, uint rs2)
        {
            this.instruction = instruction;
            this.old = old;
            this.rs2 = rs2;
        }
    }
    
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
            funct3      = (int)((instruction >> 12) & 0x7);
            funct7  = (int)((instruction >> 25) & 0x7F);
            rs1         = (int)((instruction >> 15) & 0x1F);
            rs2         = (int)((instruction >> 20) & 0x1F);
            imm     = 0;
            val_rs1 = 0;
            val_rs2 = 0;
        }
    }
    
    private struct ExecResult
    {
        public uint instruction; // Original instruction word
        
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
        
        // For atomics / CSRs / interrupts
        public uint csr;          // Destination CSR register (0 = none)
        public int rs1;
        public uint val_rs1;
        public uint val_rs2;
        public int funct3;
        public int funct7;
        public uint imm;
        public OpType op;

        // Control flow
        public bool branch_taken;
        public uint new_pc;

        public ExecResult()
        {
            instruction = 0;
            dest = 0;
            csr = 0;
            result = 0;
            write_to_memory = false;
            read_from_memory = false;
            zero_extend = false;
            mem_addr = 0;
            mem_write_val = 0;
            mem_width = 0;
            branch_taken = false;
            new_pc = 0;
            funct3 = 0;
            funct7 = 0;
            op = OpType.OTHER;
            rs1 = 0;
            val_rs1 = 0;
            val_rs2 = 0;
        }
    }

    private struct Csrs
    {
        public uint MStatus;    // 0x300
        public uint Mie;        // 0x304
        public uint Mip;        // 0x344
        
        public uint Mtvec;      // 0x305
        public uint Mepc;       // 0x341
        public uint Mcause;     // 0x342
        public uint Mtval;      // 0x343
        public uint Mscratch;   // 0x340
    }
    
    public bool halted { get; private set; }
    public bool halt_on_break { get; set; }
    
    private uint[] reg = new uint[32]; // 32 General registers
    private uint pc; // Program counter
    private uint current_ins; // Current instruction being executed

    private Csrs csrs; // Control and Status Registers
    private bool waiting_for_interrupt;
    
    private uint reservation_addr; // Reserved address for atomic operations
    private bool reservation_valid; // Is the reservation valid?

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
        halted = halt_on_break;
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
            case 0x73: // SYSTEM
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

        r.instruction = d.ins;

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
            
            case 0x2F: // Atomics
                if (d.funct3 == 0x2 && d.funct7 == 0x08) // LR.W
                {
                    if((d.val_rs1 & 0x3) != 0)
                        throw new Exception("Misaligned address for LR.W");

                    reservation_addr = d.val_rs1;
                    reservation_valid = true;
                    
                    r.dest = d.rd;
                    r.mem_addr = d.val_rs1 + d.imm;
                    r.mem_width = 4;
                    r.op = OpType.LR;
                    r.read_from_memory = true;
                }
                else if(d.funct3 == 0x2 && d.funct7 == 0x0C) // SC.W
                {
                    r.dest = d.rd;
                    r.op = OpType.SC;

                    if (reservation_valid && reservation_addr == d.val_rs1)
                    {
                        r.mem_addr = d.val_rs1 + d.imm;
                        r.mem_write_val = d.val_rs2;
                        r.mem_width = 4;
                        r.write_to_memory = true;
                        r.result = 0; // Success
                    }
                    else
                    {
                        r.result = 1; // Failure
                    }
                    reservation_valid = false;
                } else if (d.funct3 == 0x2) // AMOs 
                {
                    r.op = OpType.AMO;
                    r.funct7 = d.funct7;
                    r.val_rs2 = d.val_rs2;
                    r.dest = d.rd;
                    
                    r.mem_addr = d.val_rs1 + d.imm;
                    r.mem_width = 4;
                    r.write_to_memory = true;
                    r.read_from_memory = true;
                }
                break;
            
            case 0x33: // R type
                r.dest = d.rd;
                if (d.funct3 == 0x0 && d.funct7 == 0x0) // ADD
                {
                    r.result = d.val_rs1 + d.val_rs2;
                }
                else if (d.funct3 == 0x0 && d.funct7 == 0x20) // SUB
                {
                    r.result = d.val_rs1 - d.val_rs2;
                }
                else if (d.funct3 == 0x1 && d.funct7 == 0x00) // SLL
                {
                    r.result = (uint)((int)d.val_rs1 << (int)(d.val_rs2 & 0x1F));
                }
                else if (d.funct3 == 0x5 && d.funct7 == 0x00) // SRL
                {
                    r.result = d.val_rs1 >> (int)(d.val_rs2 & 0x1F);
                }
                else if (d.funct3 == 0x5 && d.funct7 == 0x20) // SRA
                {
                    r.result = (uint)((int)d.val_rs1 >> (int)(d.val_rs2  & 0x1F));
                }
                else if (d.funct3 == 0x2 && d.funct7 == 0x00) // SLT
                {
                    r.result = (int)d.val_rs1 < (int)d.val_rs2 ? (uint)1 : (uint)0; 
                }
                else if (d.funct3 == 0x3 && d.funct7 == 0x00) // SLTU
                {
                    r.result = d.val_rs1 < d.val_rs2 ? (uint)1 : (uint)0;
                }
                else if (d.funct3 == 0x7 && d.funct7 == 0x0) // AND
                {
                    r.result = d.val_rs1 & d.val_rs2;
                }
                else if (d.funct3 == 0x6 && d.funct7 == 0x0) // OR 
                {
                    r.result = d.val_rs1 | d.val_rs2;
                }
                else if (d.funct3 == 0x4 && d.funct7 == 0x0) // XOR
                {
                    r.result = d.val_rs1 ^ d.val_rs2;
                } 
                else if (d.funct3 == 0x0 && d.funct7 == 0x1) // MUL
                {
                    r.result = (uint)((int)d.val_rs1 * (int)d.val_rs2);
                }
                else if (d.funct3 == 0x1 && d.funct7 == 0x1) // MULH 
                {
                    r.result = (uint)(((long)(int)d.val_rs1 * (long)(int)d.val_rs2) >> 32);
                }
                else if (d.funct3 == 0x2 && d.funct7 == 0x1) // MULHSU
                {
                    r.result = (uint)(((long)(int)d.val_rs1 * (long)(uint)d.val_rs2) >> 32);
                }
                else if (d.funct3 == 0x3 && d.funct7 == 0x1) // MULHU
                {
                    r.result = (uint)(((ulong)d.val_rs1 * (ulong)d.val_rs2) >> 32);
                }
                else if (d.funct3 == 0x4 && d.funct7 == 0x1) // DIV
                {
                    if (d.val_rs2 == 0)
                    {
                        r.result = 0xFFFFFFFF; // Division by zero
                    }
                    else if ((int)d.val_rs1 == int.MinValue && (int)d.val_rs2 == -1)
                    {
                        r.result = unchecked((uint)int.MinValue); // Overflow case
                    }
                    else
                    {
                        r.result = (uint)((int)d.val_rs1 / (int)d.val_rs2);
                    }
                }
                else if (d.funct3 == 0x5 && d.funct7 == 0x1) // DIVU
                {
                    r.result = d.val_rs2 == 0 ? 0xFFFFFFFF : d.val_rs1 / d.val_rs2;
                }
                else if (d.funct3 == 0x6 && d.funct7 == 0x1) // REM
                {
                    if (d.val_rs2 == 0)
                        r.result = d.val_rs1;
                    else if ((int)d.val_rs1 == int.MinValue && (int)d.val_rs2 == -1)
                        r.result = 0;
                    else
                        r.result = (uint)((int)d.val_rs1 % (int)d.val_rs2);
                }
                else if (d.funct3 == 0x7 && d.funct7 == 0x1) // REMU
                {
                    r.result = d.val_rs2 == 0 ? d.val_rs1 : d.val_rs1 % d.val_rs2;
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
                if (d.imm == 0 && d.funct3 == 0x0) // ECALL
                {
                    uint syscall = get_register(17);
                    uint[] args = new uint[7];
                    Array.Copy(reg, 10, args, 0, 7);
                    on_syscall(syscall, args);
                }
                else if (d.imm == 1 && d.funct3 == 0x0) // EBREAK
                {
                    on_break(pc);
                }
                else if (d.funct3 == 0x0 && d.imm == 0x105) // WFI
                {
                    r.op = OpType.INTERRUPT;
                    r.imm = d.imm;
                }
                else if (d.funct3 == 0x0 && d.imm == 0x302) // MRET
                {
                    r.op = OpType.INTERRUPT;
                    r.imm = d.imm;
                }
                else if (d.funct3 == 0x1) // CSRRW
                {
                    r.op = OpType.CSR;
                    r.dest = d.rd;
                    r.csr = d.imm;
                    r.val_rs1 = d.val_rs1;
                    r.funct3 = d.funct3;
                }
                else if (d.funct3 == 0x2) // CSRRS
                {
                    r.op = OpType.CSR;
                    r.dest = d.rd;
                    r.rs1 = d.rs1;
                    r.csr = d.imm;
                    r.val_rs1 = d.val_rs1;
                    r.funct3 = d.funct3;
                }
                else if (d.funct3 == 0x3) // CSRRC
                {
                    r.op = OpType.CSR;
                    r.dest = d.rd;
                    r.csr = d.imm;
                    r.val_rs1 = d.val_rs1;
                    r.funct3 = d.funct3;
                }
                else if (d.funct3 == 0x5) // CSRRWI
                {
                    r.op = OpType.CSR;
                    r.dest = d.rd;
                    r.csr = d.imm;
                    r.funct3 = d.funct3;
                }
                else if (d.funct3 == 0x6) // CSRRSI
                {
                    r.op = OpType.CSR;
                    r.dest = d.rd;
                    r.csr = d.imm;
                    r.funct3 = d.funct3;
                }
                else if (d.funct3 == 0x7) // CSRRCI
                {
                    r.op = OpType.CSR;
                    r.dest = d.rd;
                    r.csr = d.imm;
                    r.funct3 = d.funct3;
                }
                break;
            default:
                Console.WriteLine("Unkonwn instruction opcode: 0x" + d.opcode.ToString("X"));
                break;
        }
        return r;
    }
    
    public void external_interrupt()
    {
        csrs.Mip |= MIE_MEIE;
    }
    
    public void clear_external_interrupt()
    {
        csrs.Mip &= ~MIE_MEIE;
    }

    private bool interrupt_pending()
    {
        bool global_enabled = (csrs.MStatus & MSTATUS_MIE) != 0;
        bool external_enabled = (csrs.Mie & MIE_MEIE) != 0;
        bool external_pending = (csrs.Mip & MIE_MEIE) != 0;

        return global_enabled && external_enabled && external_pending;
    }


    private uint get_interrupt_cause()
    {
        uint pending = csrs.Mie & csrs.Mip;
        if ((pending & MIE_MEIE) != 0) return 11; // Machine external interrupt
        if ((pending & MIE_MTIE) != 0) return 7;  // Machine timer interrupt
        if ((pending & MIE_MSIE) != 0) return 3;  // Machine software interrupt
        
        throw new Exception("Invalid interrupt cause");
    }

    private void handle_interrupt(uint interrupt_code)
    {
        waiting_for_interrupt = false;
        csrs.Mepc = pc & ~0x3u;
        csrs.Mcause = (1u << 31) | interrupt_code;
        
        bool mie = (csrs.MStatus & MSTATUS_MIE) != 0;
        
        if(mie) csrs.MStatus |= MSTATUS_MPIE;
        else csrs.MStatus &= ~MSTATUS_MPIE;
        
        csrs.MStatus &= ~MSTATUS_MIE;
        
        csrs.Mtval = 0;
        
        pc = csrs.Mtvec & ~0x3u;
    }
    
    private uint get_csr(uint csr)
    {
        return csr switch
        {
            CSR_MSTATUS  => csrs.MStatus,
            CSR_MIE      => csrs.Mie,
            CSR_MIP      => csrs.Mip,
            CSR_MTVEC    => csrs.Mtvec,
            CSR_MEPC     => csrs.Mepc,
            CSR_MCAUSE   => csrs.Mcause,
            CSR_MTVAL    => csrs.Mtval,
            CSR_MSCRATCH => csrs.Mscratch,
            _ => throw new Exception($"Read from unsupported CSR 0x{csr:X3}")
        };
    }
    
    private void set_csr(uint csr, uint value)
    {
        switch (csr)
        {
            case CSR_MSTATUS:
                // Only allow MIE and MPIE for now
                csrs.MStatus =
                    value & (MSTATUS_MIE | MSTATUS_MPIE);
                break;

            case CSR_MIE:
                csrs.Mie =
                    value & (MIE_MSIE | MIE_MTIE | MIE_MEIE);
                break;

            case CSR_MIP:
                // Usually read-only
                csrs.Mip =
                    value & (MIE_MSIE | MIE_MTIE | MIE_MEIE);
                break;

            case CSR_MTVEC:
                // Must be aligned, mode bits ignored
                csrs.Mtvec = value & ~0x3u;
                break;

            case CSR_MEPC:
                csrs.Mepc = value;
                break;

            case CSR_MCAUSE:
                // mcause is typically read-only
                break;

            case CSR_MTVAL:
                csrs.Mtval = value;
                break;

            case CSR_MSCRATCH:
                csrs.Mscratch = value;
                break;

            default:
                throw new Exception($"Write to unsupported CSR 0x{csr:X3}");
        }
    }


    private void csr_op(CsrIns c)
    {
        uint old_csr = get_csr(c.csr);
        
        switch (c.instruction)
        {
            case 0x1: // CSRRW
                set_csr(c.csr, c.val_rs1);
                if (c.rd != 0)
                    set_reg(c.rd, old_csr);
                break;
            
            case 0x2: // CSRRS
                if(c.val_rs1 != 0)
                    set_csr(c.csr, old_csr | c.val_rs1);
                if (c.rd != 0)
                    set_reg(c.rd, old_csr);
                break;
            
            case 0x3: // CSRRC
                if(c.val_rs1 != 0)
                    set_csr(c.csr, old_csr & ~c.val_rs1);
                if (c.rd != 0)
                    set_reg(c.rd, old_csr);
                break;
            
            case 0x5: // CSRRWI
                set_csr(c.csr, c.zimm);
                if (c.rd != 0)
                    set_reg(c.rd, old_csr);
                break;
            
            case 0x6: // CSRRSI
                if(c.zimm != 0)
                    set_csr(c.csr, old_csr | c.zimm);
                if (c.rd != 0)
                    set_reg(c.rd, old_csr);
                break;
                
            case 0x7: // CSRRCI
                if(c.zimm != 0)
                    set_csr(c.csr, old_csr & ~c.zimm);
                if(c.rd != 0)
                    set_reg(c.rd, old_csr);
                break;
        }
    }

    private void interrupt_op(uint instruction)
    {
        switch (instruction)
        {
            case 0x105: // WFI
                waiting_for_interrupt = true;
                break;
            
            case 0x302: // MRET
                bool mpie = (csrs.MStatus & MSTATUS_MPIE) != 0;
                if(mpie) csrs.MStatus |= MSTATUS_MIE;
                else csrs.MStatus &= ~MSTATUS_MIE;
                
                csrs.MStatus |= MSTATUS_MPIE;

                pc = csrs.Mepc;
                break;
        }
    }

    private uint atomic_op(AtomicIns a)
    {
        switch (a.instruction)
        {
            case 0x00: // AMOADD.W
                return a.old+a.rs2;
            case 0x04: // AMOSWAP.W
                return a.rs2;
            case 0x10: // AMOXOR.W
                return a.old ^ a.rs2;
            case 0x20: // AMOOR.W
                return a.old | a.rs2;
            case 0x30: // AMOAND.W
                return a.old & a.rs2;
            case 0x40: // AMOMIN.W
                return (int)a.old < (int)a.rs2 ? a.old : a.rs2;
            case 0x50: // AMOMAX.W
                return (int)a.old > (int)a.rs2 ? a.old : a.rs2;
            case 0x60: // AMOMINU.W
                return (uint)a.old < (uint)a.rs2 ? a.old : a.rs2;
            case 0x70: // AMOMAXU.W
                return (uint)a.old > (uint)a.rs2 ? a.old : a.rs2;
        }
        return 0;
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
        if (r.op == OpType.LR)
        {
            r.result = sign_extend(bus.read(r.mem_addr, r.mem_width), r.mem_width);
        }
        else if (r.op == OpType.SC)
        {
            if (r.write_to_memory)
            {
                bus.write(r.mem_addr, r.mem_write_val, r.mem_width);
                reservation_valid = false;
            }
        }
        else if (r.op == OpType.AMO)
        {
            uint old_val = bus.read(r.mem_addr, r.mem_width);
            uint new_val = atomic_op(new AtomicIns(r.funct7, old_val, r.val_rs2));
            
            bus.write(r.mem_addr, new_val, r.mem_width);
            reservation_valid = false;
            
            r.result = old_val;
        }
        else if (r.op == OpType.CSR)
        {
            csr_op(new CsrIns(r.funct3, r.val_rs1, r.csr, r.dest, (r.instruction >> 15) & 0x1F));
        }
        else if (r.op == OpType.INTERRUPT)
        {
            interrupt_op(r.imm);
        }
        else if (r.op == OpType.OTHER)
        {
            if (r.read_from_memory) {
                r.result = r.zero_extend ? bus.read(r.mem_addr, r.mem_width) : sign_extend(bus.read(r.mem_addr, r.mem_width), r.mem_width);
            }
            if (r.write_to_memory) {
                bus.write(r.mem_addr, r.mem_write_val, r.mem_width);
                reservation_valid = false;
            }
        }
        
        if (r.dest != 0 && r.op != OpType.CSR && r.op != OpType.INTERRUPT) set_reg(r.dest, r.result); // Cannot write to 0x0
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

        bus.clear_interrupt += clear_external_interrupt;
        bus.request_interrupt += external_interrupt;

        halted = false;
        halt_on_break = false;
        
        reg[0] = 0; // Register 0x0 is always zero and cannot be written to
        pc = 0;
        reservation_addr = 0;
        reservation_valid = false;

        waiting_for_interrupt = false;
        csrs = new Csrs();
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

    public uint[] get_registers()
    {
        return reg;
    }
    
    public void set_pc(uint address)
    {
        pc = address;
    }
    
    public uint get_pc()
    {
        return pc;
    }
    
    public uint get_current_instruction()
    {
        return current_ins;
    }
    
    public void halt()
    {
        halted = true;
    }
    
    public void resume()
    {
        halted = false;
    }

    public void step()
    {
        if (halted) return;
        if(interrupt_pending()) handle_interrupt(get_interrupt_cause());
        if (waiting_for_interrupt) return;
        
        current_ins = bus.read(pc, 4); // Fetch
        uint current_pc = pc;
        pc += 4; // Increment PC by 4 bytes
        
        var decoded = decode(current_ins);
        var exec = execute(decoded, current_pc);
        write_back(exec);
    }

    public void run_until_halt()
    {
        while (!halted)
        {
            step();
        }
    }
}