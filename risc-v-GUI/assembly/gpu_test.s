    .include "bios_macros.s"
    
    .section .data
target_file: .ascii "LINUX   PNG"
error_msg: .ascii "File not found!\n\0"
newline: .ascii "\n\0"
.equ MMIO_GPU, 0x500024

.section .text
    .globl main
    .global _start
    
main:
    la a0, heap_start
    jal ra, bios_putx
    
    la a0, newline
    jal ra, bios_puts

    jal ra, bios_init
    
    la a0, target_file
    jal ra, bios_find
    
    beqz a0, file_not_found
    
    PUSH a1
    
    la s11, heap_start
    
    mv a1, s11
    jal ra, bios_load
    
    POP a0
    PUSH a0
    jal ra, bios_putu
    
    POP a0
    
    la t0, MMIO_GPU
    sw s11, 0x00(t0)           # Write starting memory address of bmp
    sw a0, 0x04(t0)            # Write file length
    li t1, 1
    sw t1, 0x08(t0)            # Write go command for drawing BMP 
    
    ebreak
    
file_not_found:
    la a0, error_msg
    jal ra, bios_puts
    ebreak
    