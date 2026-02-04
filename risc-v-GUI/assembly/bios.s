    .section .text
    .globl bios_putc
    .globl bios_puth
    .globl bios_putw
    .globl bios_putu
    .globl bios_puts
    .globl bios_clear_interrupt

# MMIO console
.equ MMIO_CONSOLE, 0x20000000

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
    li   t0, MMIO_CONSOLE   # console address in t0
1:  lb   t1, 0(a0)          # load byte from string
    beq  t1, zero, 2f       # if null terminator, done
    sb   t1, 0(t0)          # write byte to console
    addi a0, a0, 1          # move pointer to next char
    j    1b
2:  ret

# --------------------------------------------------
# bios_putu(uint32 value)
# prints unsigned integer in decimal (no stack, no nested calls)
# Arg: a0 contains value
# Clobbers: t0,t1,t2,t3,t5
# Uses digit_buf (bss) as temporary buffer
# --------------------------------------------------
bios_putu:
    li   t0, 10        # divisor
    li   t1, 0         # digit count
    la   t2, digit_buf # pointer into buffer

1:  remu t3, a0, t0
    divu a0, a0, t0
    addi t3, t3, '0'
    sb   t3, 0(t2)
    addi t2, t2, 1
    addi t1, t1, 1
    bnez a0, 1b

    li   t5, MMIO_CONSOLE

2:  addi t2, t2, -1
    lb   t3, 0(t2)
    sb   t3, 0(t5)      # direct MMIO write (no call)
    addi t1, t1, -1
    bnez t1, 2b

    ret
    
# --------------------------------------------------
# bios_clear_interrupt()
# Clears the interrupt in the BIOS console
# Clobbers: t0, t1
# --------------------------------------------------
bios_clear_interrupt:
    li   t0, MMIO_CONSOLE
    li   t1, 0x00000000
    sw   t1, 4(t0)      # write to interrupt clear register
    
    ret

    .section .bss
    .align 4
digit_buf:
    .space 12
