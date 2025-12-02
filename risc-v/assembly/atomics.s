    .section .data
# One word per test, nice spaced addresses
mem_add:    .word  100        # test AMOADD (old=100)
mem_swap:   .word  7          # test AMOSWAP (old=7)
mem_xor:    .word  0xFF00FF00 # test AMOXOR
mem_or:     .word  0x0F0F0F0F # test AMOOR
mem_and:    .word  0xFFFFFFFF # test AMOAND
mem_min:    .word  -5         # test AMOMIN (signed)
mem_max:    .word  10         # test AMOMAX (signed)
mem_minu:   .word  0x00000005 # test AMOMINU (unsigned)
mem_maxu:   .word  0xFFFFFFF0 # test AMOMAXU (unsigned)

# LR/SC test memory
mem_lrsc:   .word  42

    .section .text
    .global _start
_start:

    # ---------- AMOADD.W ----------
    la   x10, mem_add       # x10 -> mem_add
    li   x11, 25            # x11 = rs2 for AMOADD
    amoadd.w x12, x11, (x10) # x12 = old (100)
    lw   x13, 0(x10)         # x13 = new mem (125)

    # ---------- AMOSWAP.W ----------
    la   x10, mem_swap
    li   x11, 123
    amoswap.w x14, x11, (x10) # x14 = old (7)
    lw      x15, 0(x10)       # x15 = new mem (123)

    # ---------- AMOXOR.W ----------
    la   x10, mem_xor
    li   x11, 0x00FF00FF
    amoxor.w x16, x11, (x10)  # x16 = old (0xFF00FF00)
    lw    x17, 0(x10)         # x17 = new mem (0xFFFFFFFF)

    # ---------- AMOOR.W ----------
    la   x10, mem_or
    li   x11, 0xF0F0F0F0
    amoor.w x18, x11, (x10)   # x18 = old (0x0F0F0F0F)
    lw   x19, 0(x10)          # x19 = new mem (0xFFFFFFFF)

    # ---------- AMOAND.W ----------
    la   x10, mem_and
    li   x11, 0x0F0F0F0F
    amoand.w x20, x11, (x10)  # x20 = old (0xFFFFFFFF)
    lw   x21, 0(x10)          # x21 = new mem (0x0F0F0F0F)

    # ---------- AMOMIN.W (signed) ----------
    la   x10, mem_min
    li   x11, 5               # compare -5 and 5 -> min = -5 (old)
    amomin.w x22, x11, (x10)  # x22 = old (-5)
    lw   x23, 0(x10)          # x23 = new mem (-5) (unchanged)

    # ---------- AMOMAX.W (signed) ----------
    la   x10, mem_max
    li   x11, 20
    amomax.w x24, x11, (x10)  # x24 = old (10)
    lw   x25, 0(x10)          # x25 = new mem (20)

    # ---------- AMOMINU.W (unsigned min) ----------
    la   x10, mem_minu
    li   x11, 3
    amominu.w x26, x11, (x10) # x26 = old (5)
    lw   x27, 0(x10)          # x27 = new mem (3)

    # ---------- AMOMAXU.W (unsigned max) ----------
    la   x10, mem_maxu
    li   x11, 0x000000FF
    amomaxu.w x28, x11, (x10) # x28 = old (0xFFFFFFF0)
    lw   x29, 0(x10)          # x29 = new mem (0xFFFFFFFF? depends on max unsigned: max(0xFFFFFFF0,0xFF)=0xFFFFFFF0)

    # ---------- LR/SC success then failure ----------
    la   x10, mem_lrsc
    lr.w x30, (x10)            # x30 = old (42), reservation set
    addi x31, x30, 1           # x31 = x30 + 1 (43)
    sc.w x5, x31, (x10)        # x5 = 0 on success, mem becomes 43
    # Now try SC without LR -> must fail
    li  x6, 99
    sc.w x7, x6, (x10)         # x7 = 1 (fail), mem remains 43
    lw  x8, 0(x10)             # x8 = mem final (43)

    # ---- stop and inspect ----
    ebreak
