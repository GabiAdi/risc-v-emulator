.data
msg: .string "Hello, World!\n"

.text
.globl _start
_start:
    la a0, msg       # Load address of string into a0
    li a7, 4         # Syscall 4 = print string
    ecall            # print "Hello, World!\n"

    ebreak           # Trigger a breakpoint for debugging

    li a0, 0         # Exit code 0
    li a7, 10        # Syscall 10 = exit
    ecall            # Exit cleanly
