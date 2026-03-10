    .include "bios_macros.s"
    
    .section .data
target_file: .ascii "IMAGE   BMP"
error_msg: .ascii "File not found!\n\0"
success_msg: .ascii "File read successfully! File size: \n\0"
init_msg: .ascii "Saving file at address: \0"
newline: .ascii "\n\0"

.section .text
    .globl main
    .global _start
    
main:
    la a0, init_msg
    jal ra, bios_puts
    
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

    la a0, success_msg
    jal ra, bios_puts
    
    POP a0
    jal ra, bios_putu
    
    #la a0, heap_start
    #jal ra, bios_puts
    
    ebreak
    
file_not_found:
    la a0, error_msg
    jal ra, bios_puts
    ebreak
    