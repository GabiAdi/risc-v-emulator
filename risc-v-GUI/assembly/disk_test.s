    .include "bios_macros.s"
    
    .section .data
target_file: .ascii "TEST    TXT"
error_msg: .ascii "File not found!\n\0"
success_msg: .ascii "File read successfully!\n\0"

.section .text
    .globl main
    .global _start
    
main:
    # la t0, heap_start

    jal ra, bios_init
    
    la a0, target_file
    jal ra, bios_find
    
    beqz a0, file_not_found
    
    la s11, heap_start
    li t0, 0x1000
    add s11, s11, t0

    mv a1, s11
    jal ra, bios_load

    la a0, success_msg
    jal ra, bios_puts
    
    ebreak
    
file_not_found:
    la a0, error_msg
    jal ra, bios_puts
    ebreak
    