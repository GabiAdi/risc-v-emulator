    .include "bios_macros.s"
   
    .section .text
    .globl bios_putc
    .globl bios_puth
    .globl bios_putw
    .globl bios_putu
    .globl bios_puts
    .globl bios_clin
    .globl bios_getc
    .globl _start

# MMIO console
.equ MMIO_CONSOLE, 0x20000000

_start:
    la sp, stack_top
    jal main

# --------------------------------------------------
# bios_putc(char c)
# a0 = character (low byte)
# Clobbers: t0
# Returns: ret (uses ra)
# --------------------------------------------------
bios_putc:
    li   t0, MMIO_CONSOLE
    sb   a0, 0(t0)
    ret

# --------------------------------------------------
# bios_puth(uint16)
# a0 = packed 2 chars (low half is first char)
# Clobbers: t0
# --------------------------------------------------
bios_puth:
    li   t0, MMIO_CONSOLE
    sh   a0, 0(t0)
    ret

# --------------------------------------------------
# bios_putw(uint32)
# a0 = packed 4 chars (LSB first)
# Clobbers: t0
# --------------------------------------------------
bios_putw:
    li   t0, MMIO_CONSOLE
    sw   a0, 0(t0)
    ret
    
# --------------------------------------------------
# bios_puts(char* str)
# prints null-terminated string
# Arg: a0 = pointer to string
# Clobbers: t0, t1
# Returns: ret (ra preserved)
# --------------------------------------------------
bios_puts:
    PUSH ra
    PUSH s0
    
    mv s0, a0

1:  lb   a0, 0(s0)          # load byte from string
    beq  a0, zero, 2f       # if null terminator, done
    jal bios_putc           # print character
    addi s0, s0, 1          # move pointer to next char
    j    1b
2:  POP s0
    POP ra
    ret

# --------------------------------------------------
# bios_putu(uint32 value)
# prints unsigned integer in decimal (no stack, no nested calls)
# Arg: a0 contains value
# Clobbers: t0,t1,t2,t3,t5
# Uses digit_buf (bss) as temporary buffer
# --------------------------------------------------
bios_putu:
    PUSH ra
    li   t0, 10        # divisor
    li   t1, 0         # digit count

1:  remu t2, a0, t0
    divu a0, a0, t0
    addi t2, t2, '0'
    PUSH t2
    addi t1, t1, 1
    bnez a0, 1b

2:  POP a0
    PUSH t1
    jal bios_putc
    POP t1
    addi t1, t1, -1
    bnez t1, 2b

    POP ra
    ret
    
# --------------------------------------------------
# bios_clear_interrupt()
# Clears the interrupt in the BIOS console
# Clobbers: t0, t1
# --------------------------------------------------
bios_clin:
    li   t0, MMIO_CONSOLE
    li   t1, 0x00000000
    sw   t1, 4(t0)      # write to interrupt clear register
    
    ret

# --------------------------------------------------
# bios_getc()
# Reads a character from the console input register
# Clobbers: t0
# Returns: a0 = character read (low byte)
# --------------------------------------------------
bios_getc:
    li   t0, MMIO_CONSOLE
    lb   a0, 8(t0)      # read from input register
    ret

    .section .bss
    .align 4
digit_buf:
    .space 12
    
stack_bottom:
    .space 1024
stack_top:
