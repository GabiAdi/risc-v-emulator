.section .data
mem:
    .word 42      # initial value

.section .text
.global _start

_start:
    la x10, mem       # x10 = &mem (pointer to memory)

    # First LR/SC → should succeed
    lr.w x11, (x10)   # x11 = 42 (loaded)
    li x12, 100       # x12 = new value to store
    sc.w x13, x12, (x10) # SC.W → should succeed, x13 = 0
                         
    # Second SC.W without a new LR → should fail
    li x14, 200       # x14 = attempted store value
    sc.w x15, x14, (x10) # SC.W → should fail, x15 = 1

    ebreak             # inspect registers and memory
