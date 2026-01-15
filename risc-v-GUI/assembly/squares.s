        .section .data
newline: 
        .word 10          # newline character

        .section .text
        .globl _start
_start:
        li      t0, 0          # counter i = 0

loop:
        mul     t1, t0, t0     # t1 = i*i

        # Print t1 as integer
        mv      a0, t1         # syscall expects value in a0
        li      a7, 1          # syscall code 1 = print integer
        ecall

        # Print newline
        li      a0, 10         # newline character
        li      a7, 11         # syscall code 11 = print char
        ecall

        addi    t0, t0, 1      # i++
        li      t2, 16         # upper limit
        sub     t3, t2, t0
        bnez    t3, loop       # loop if i < 16

        # Pause for inspection
        ebreak

        # Exit program
        li      a7, 10         # syscall 10 = exit
        ecall
