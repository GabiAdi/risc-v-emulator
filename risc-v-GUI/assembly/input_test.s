    .include "bios_macros.s"
    
    .section .data
counter_val:
    .word 0

test:
    .string "Hello, World!\n\0"
    .byte 0

.section .text
    .globl main
    .globl trap_handler
    
reg_save_area:
    .word 0, 0, 0 # space for t0, t1, t2

main:
    # Set mtvec to trap handler
    la t0, trap_handler
    csrw mtvec, t0
    
    li t1, 8
    csrs mstatus, t1
    
    li t1, 1 << 11
    csrs mie, t1

    # Initialize counter
    li t0, 0            # s0 = counter
    
    ebreak

loop:
    addi t0, t0, 1
    # wfi
    j loop

# ----------------------------
# Machine-mode trap/interrupt handler
# ----------------------------
.align 4
trap_handler:
    PUSH t0

    jal ra, bios_getc  # read character from console input
    jal ra, bios_putc  # echo character back to console output
    jal ra, bios_clin  # clear interrupt in BIOS
        
    # Clear pending external interrupt
    li t0, 11           # MEIP bit = 11
    csrrc zero, mip, t0
    
    POP t0
    
    ebreak

    mret
