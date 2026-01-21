    .section .data
counter_val:
    .word 0

    .section .text
    .globl _start
    .globl trap_handler

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

loop:
    addi t1, t1, 1
    sw t1, 0(t2)       # store counter in memory
    # wfi
    j loop

# ----------------------------
# Machine-mode trap/interrupt handler
# ----------------------------
.align 4
trap_handler:
    la t2, counter_val  # reload address of counter_val
    lw a0, 0(t2)        # load counter into a0

    li a7, 1            # syscall 1 = print integer
    ecall

    # Clear pending external interrupt
    li t0, 11           # MEIP bit = 11
    csrrc zero, mip, t0

    mret
