.section .data
msg: 
    .string "Hello, World!\n\0"
    .byte 0

.text
.globl main
main:
    la a0, msg    
    jal ra, bios_puts

    ebreak           # Trigger a breakpoint for debugging

    li a0, 0         # Exit code 0
    li a7, 10        # Syscall 10 = exit
    ecall            # Exit cleanly
