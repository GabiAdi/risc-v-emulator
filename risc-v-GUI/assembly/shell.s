    .include "bios_macros.s"
    .include "bios_symbols.s"
    
    .section .data
newline: .ascii "\n\0"
cmd_help: .ascii "HELP\0"
cmd_ls: .ascii "LS\0"
cmd_cat: .ascii "CAT\0"
msg_help: .ascii "Help:\nhelp - prints this help\nls - prints all files\n\0"
msg_unknown: .ascii "Unkown command\n\0"
msg_cat_not_found: .ascii "File not found\n\0"
msg_cat_usage: .asciz "Usage: cat <filename>\n\0"
msg_shell: .ascii "/$ \0"

.section .text
    .globl _start
    .globl trap_handler
    .globl heap_start
    
_start:
    la t0, trap_handler
    csrw mtvec, t0
    
    li t1, 8
    csrs mstatus, t1
    
    li t1, 1 << 11
    csrs mie, t1
    
    la s0, input_buf
    la s1, input_len
    sw zero, 0(s1)
    
    jal ra, bios_init
    
    la a0, msg_shell
    jal ra, bios_puts
    
loop:
    wfi
    j loop
    
    
.align 4
trap_handler:
    PUSH ra; PUSH s0; PUSH s1; PUSH s2
    
    la s0, input_buf
    la s1, input_len
    lw s2, 0(s1)
   
    jal ra, bios_getc
    
    li t0, 10
    beq a0, t0, enter_pressed
    
    jal ra, bios_putc
    
    li t0, 63
    bge s2, t0, interrupt_done # If buffer full, discard
    
    add t1, s0, s2
    sb a0, 0(t1)
    addi s2, s2, 1
    sw s2, 0(s1)
    j interrupt_done
    
enter_pressed:
    add t0, s0, s2
    sb zero, 0(t0)
    
    beqz s2, no_cmd
    
    la a0, newline
    jal ra, bios_puts
    
    # HELP
    mv a0, s0
    la a1, cmd_help
    jal ra, bios_strcmp
    beqz a0, do_help
    
    # LS
    mv a0, s0
    la a1, cmd_ls
    jal ra, bios_strcmp
    beqz a0, do_ls
    
    #CAT
    mv a0, s0
    la a1, cmd_cat
    li a2, 3
    jal ra, bios_strncmp
    beqz a0, do_cat

    la a0, msg_unknown
    jal ra, bios_puts
    
    j clear_buffer

do_help:
    la a0, msg_help
    jal ra, bios_puts
    j clear_buffer
    
do_ls:
    la a0, print_entry
    jal ra, bios_ls
    j clear_buffer

do_cat:
    PUSH s0; PUSH s1;
    la a0, input_buf
    addi a0, a0, 3
    lbu t0, 0(a0)
    li t1, ' '
    bne t0, t1, cat_no_arg
    addi a0, a0, 1
    
    la a1, fat_name_buf
    jal ra, str_to_fat83
    
    la a0, fat_name_buf
    jal ra, bios_find
    
    beqz a0, cat_not_found
    
    PUSH a1
    
    la a1, heap_start
    jal ra, bios_load
    
    POP s1
    la s0, heap_start
    
cat_print_loop:
    beqz s1, cat_done
    lbu a0, 0(s0)
    jal ra, bios_putc
    addi s0, s0, 1
    addi s1, s1, -1
    j cat_print_loop

cat_no_arg:
    la a0, msg_cat_usage
    jal ra, bios_puts
    j clear_buffer

cat_not_found:
    la a0, msg_cat_not_found
    jal ra, bios_puts

cat_done:
    POP s1; POP s0;
    j clear_buffer
    
no_cmd:
    la a0, newline
    jal ra, bios_puts
    j clear_buffer
    
clear_buffer:
    la a0, msg_shell
    jal ra, bios_puts
    sw zero, 0(s1)

interrupt_done:
    jal ra, bios_clin
    
    POP s2; POP s1; POP s0; POP ra

    li t0, 11           # MEIP bit = 11
    csrrc zero, mip, t0
    
    mret
    
# --------------------------------------------------
# print_entry(char* name, uint32 size)
# Prints a FAT 8.3 filename and size
# a0 = pointer to 11-byte name, a1 = file size
# --------------------------------------------------
print_entry:
    PUSH ra
    PUSH s0; PUSH s1

    mv   s0, a0              # s0 = name pointer
    mv   s1, a1              # s1 = file size

    # print 8 char filename
    li   t1, 0
print_name_loop:
    add  t0, s0, t1
    lbu  a0, 0(t0)
    li   t2, ' '
    beq  a0, t2, print_dot   # stop at padding spaces
    jal ra, bios_putc
    addi t1, t1, 1
    li   t2, 8
    blt  t1, t2, print_name_loop

print_dot:
    # print dot separator
    li   a0, '.'
    jal ra, bios_putc

    # print 3 char extension
    li   t1, 8
print_ext_loop:
    add  t0, s0, t1
    lbu  a0, 0(t0)
    li   t2, ' '
    beq  a0, t2, print_size  # stop at padding spaces
    jal ra, bios_putc
    addi t1, t1, 1
    li   t2, 11
    blt  t1, t2, print_ext_loop

print_size:
    # print size
    li   a0, ' '
    jal ra, bios_putc
    mv   a0, s1
    jal ra, bios_putu

    li   a0, 10              # newline
    jal ra, bios_putc

    POP s1; POP s0
    POP ra
    ret

# --------------------------------------------------
# str_to_fat83(char* src, char* dst)
# Converts "hello.txt" to "HELLO   TXT" (FAT 8.3)
# a0 = input string, a1 = output buffer (11 bytes)
# Clobbers: t0, t1, t2, t3
# --------------------------------------------------
str_to_fat83:
    PUSH ra

    # fill output with spaces first
    mv   t3, a1
    li   t2, 11
fat83_fill:
    sb   zero, 0(t3)         # will overwrite with spaces below
    li   t0, ' '
    sb   t0, 0(t3)
    addi t3, t3, 1
    addi t2, t2, -1
    bnez t2, fat83_fill

    # copy up to 8 chars of name, uppercasing, stop at '.' or null
    li   t2, 0               # index into dst name part
fat83_name:
    lbu  t0, 0(a0)
    beqz t0, fat83_done      # end of string
    li   t1, '.'
    beq  t0, t1, fat83_ext   # hit dot, move to extension

    # uppercase: if 'a'-'z' subtract 32
    li   t1, 'a'
    blt  t0, t1, fat83_store_name
    li   t1, 'z'
    bgt  t0, t1, fat83_store_name
    addi t0, t0, -32

fat83_store_name:
    add  t1, a1, t2
    sb   t0, 0(t1)
    addi t2, t2, 1
    addi a0, a0, 1
    li   t1, 8
    blt  t2, t1, fat83_name
    j    fat83_skip_to_dot   # name full, skip to dot

fat83_skip_to_dot:
    lbu  t0, 0(a0)
    beqz t0, fat83_done
    li   t1, '.'
    beq  t0, t1, fat83_ext
    addi a0, a0, 1
    j    fat83_skip_to_dot

fat83_ext:
    addi a0, a0, 1           # skip the dot
    li   t2, 0               # index into dst ext part (starts at byte 8)
fat83_ext_loop:
    lbu  t0, 0(a0)
    beqz t0, fat83_done

    # uppercase
    li   t1, 'a'
    blt  t0, t1, fat83_store_ext
    li   t1, 'z'
    bgt  t0, t1, fat83_store_ext
    addi t0, t0, -32

fat83_store_ext:
    addi t1, a1, 8
    add  t1, t1, t2
    sb   t0, 0(t1)
    addi t2, t2, 1
    addi a0, a0, 1
    li   t1, 3
    blt  t2, t1, fat83_ext_loop

fat83_done:
    POP ra
    ret

    .section .bss
    .align 4
    
input_buf: .space 64
input_len: .space 4
fat_name_buf: .space 12
heap_start:
    .space 0x100000
    