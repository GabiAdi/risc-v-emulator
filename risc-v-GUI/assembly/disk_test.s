    .include "bios_macros.s"
    
    .section .data
    
.section .text
    .globl main
    
main:
    li a0, 0x0
    la a1, heap_start
    jal ra, bios_rsec  # read sector 0 into memory at heap_start
    
    ebreak
    
    li a0, 0x0
    la a1, heap_start
    addi a1, a1, 512
    jal ra, bios_wsec
    
    ebreak
    
    li a0, 0x0
    la a1, heap_start
    jal ra, bios_wsec
    
    ebreak
    