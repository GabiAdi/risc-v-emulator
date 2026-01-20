    .section .text
    .globl _start

_start:
    # ----------------------------
    # Test CSRRW
    # ----------------------------

    li t0, 0x8                # MSTATUS.MIE
    csrrw t1, mstatus, t0     # t1 = old mstatus, mstatus = 0x8

    csrrs t2, mstatus, x0     # t2 = mstatus (read-only test)

    # ----------------------------
    # Test CSRRS (set bits)
    # ----------------------------

    li t3, 0x80               # MPIE
    csrrs t4, mstatus, t3     # set MPIE

    # ----------------------------
    # Test CSRRC (clear bits)
    # ----------------------------

    li t5, 0x8                # clear MIE
    csrrc t6, mstatus, t5

    # ----------------------------
    # Test immediate CSR ops
    # ----------------------------

    csrrwi t0, mscratch, 0x1F # write immediate
    csrrsi t1, mscratch, 0x01 # set bit 0
    csrrci t2, mscratch, 0x01 # clear bit 0

    # ----------------------------
    # Test rd = x0 (discard)
    # ----------------------------

    csrrw x0, mscratch, t0    # write, discard readback

    # ----------------------------
    # Fake trap return test
    # ----------------------------

    la t0, after_mret
    csrw mepc, t0

    li t1, 0x80               # MPIE
    csrs mstatus, t1

    mret

after_mret:
    # If mret worked, execution resumes here

done:
    ebreak
