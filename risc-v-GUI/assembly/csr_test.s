    .section .text
    .globl _start

_start:
    # ----------------------------
    # CSRRW - read old, write new
    # ----------------------------
    li t0, 0x8                  # Value to write
    csrrw t1, mstatus, t0       # t1 = old mstatus (0), mstatus = 0x8

    # ----------------------------
    # CSRRS - set bits using rs1
    # ----------------------------
    li t2, 0x80                 # Bit mask (MPIE)
    csrrs t3, mstatus, t2       # t3 = old mstatus, mstatus |= 0x80 → 0x88

    # ----------------------------
    # CSRRC - clear bits using rs1
    # ----------------------------
    li t4, 0x8                  # Bit mask (clear MIE)
    csrrc t5, mstatus, t4       # t5 = old mstatus (0x88), mstatus &= ~0x8 → 0x80

    # ----------------------------
    # Immediate CSR operations
    # ----------------------------
    csrrwi t6, mscratch, 0x1F   # t6 = old mscratch (0), mscratch = 0x1F
    csrrsi t3, mscratch, 0x01   # set bit 0, t3 = old mscratch (0x1F), mscratch = 0x1F
    csrrci t0, mscratch, 0x01   # clear bit 0, t0 = old mscratch (0x1F), mscratch = 0x1E

    # ----------------------------
    # CSR with x0 as rd (discard old value)
    # ----------------------------
    csrrw x0, mscratch, t0       # write t0 (0x1E) into mscratch, discard old value

    # ----------------------------
    # CSR with x0 as rs1 (no write)
    # ----------------------------
    csrrs t1, mstatus, x0        # read-only, t1 = mstatus (0x80)

    # ----------------------------
    # End of program - loop forever
    # ----------------------------
    
done:
    ebreak
