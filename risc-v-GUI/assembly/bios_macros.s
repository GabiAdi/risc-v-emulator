# bios_macros.include

.macro PUSH  reg
    addi sp, sp, -4
    sw \reg, 0(sp)
.endm

.macro POP  reg
    lw \reg, 0(sp)
    addi sp, sp, 4
.endm

.macro bios_call func
    li  t0, \func
    jalr ra, t0, 0
.endm
