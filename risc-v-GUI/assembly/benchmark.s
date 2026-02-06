.section .data
start_msg: 
    .string "Started!\n\0"
    .byte 0
    
end_msg:
    .string "Finished!\n\0"
    .byte 0

.text
.globl _start
_start:
    la a0, start_msg
    li a7, 4
    ecall
    
    li t0, 100000000
    
loop:
    addi t0, t0, -1
    bnez t0, loop
    
    la a0, end_msg
    li a7, 4
    ecall

    ebreak
    
    li a0, 0         # Exit code 0
    li a7, 10        # Syscall 10 = exit
    ecall            # Exit cleanly
