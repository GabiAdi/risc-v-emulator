    .section .text
    .globl _start

_start:
    # Print "work"
    li a0, 0x6B726F77     # "work"
    jal ra, bios_putw

    # Print newline
    li a0, '\n'
    jal ra, bios_putc

    ebreak
    
    # Print number 12345
    li a0, 12345
    jal ra, bios_putu
    
    # Print newline
    li a0, '\n'
    jal ra, bios_putc
    
    ebreak
    
    # Print "Hello, World!\n" using bios_puts
    la a0, msg
    jal ra, bios_puts

done:
    ebreak

    .section .data
msg:
    .ascii "Hello, World!\n"
    .byte 0  # null terminator
    