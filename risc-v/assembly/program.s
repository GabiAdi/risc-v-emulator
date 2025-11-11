    .text
    .globl _start
_start:
    # Initialize values
    li x1, 0x00000006     # rs1 = 6 
    li x2, 0xFFFFFFFC     # rs2 = -4 (signed)
    li x3, 4              # rs3 = 4 (unsigned copy)

    # ----- Multiplication -----
    mul    x10, x1, x2    # x10 = 6 * -4 = -24
    mulh   x11, x1, x2    # high signed part of 6 * -4
    mulhsu x12, x2, x3    # signed * unsigned high
    mulhu  x13, x1, x3    # unsigned high part

    # ----- Division -----
    div    x14, x1, x2    # signed 6 / -4 = -1
    divu   x15, x1, x3    # unsigned 6 / 4 = 1

    # ----- Remainders -----
    rem    x16, x1, x2    # signed 6 % -4 = 2
    remu   x17, x1, x3    # unsigned 6 % 4 = 2

    # Stop here (ebreak can signal test end)
    ebreak
