    .section .data
counter_val:
    .word 0

    .section .text
    .globl _start
    .globl trap_handler
    
reg_save_area:
    .word 0, 0, 0 # space for t0, t1, t2

_start:
    # Set mtvec to trap handler
    la t0, trap_handler
    csrw mtvec, t0
    
    li t1, 8
    csrs mstatus, t1
    
    li t1, 1 << 11
    csrs mie, t1

    # Initialize counter
    li t1, 0          # t1 = counter
    la t2, counter_val  # t2 = address of counter_val
    
    ebreak

loop:
    addi t1, t1, 1
    # wfi
    j loop

# ----------------------------
# Machine-mode trap/interrupt handler
# ----------------------------
.align 4
trap_handler:
    la a5, reg_save_area
    sw t0, 0(a5)      # save t0
    sw t1, 4(a5)      # save t1
    sw t2, 8(a5)      # save t2
    
    
    jal ra, bios_getc  # read character from console input
    jal ra, bios_putc  # echo character back to console output
    jal ra, bios_clear_interrupt  # clear interrupt in BIOS
        
    la a5, reg_save_area
    lw t0, 0(a5)      # restore t0
    lw t1, 4(a5)      # restore t1
    lw t2, 8(a5)      # restore t2
        
    # Clear pending external interrupt
    li t0, 11           # MEIP bit = 11
    csrrc zero, mip, t0
    
    ebreak

    mret
