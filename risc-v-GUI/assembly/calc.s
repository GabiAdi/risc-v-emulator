    .include "bios_macros.s"
    .include "bios_symbols.s"

.section .data
msg_num1:   .asciz "Enter first number: "
msg_op:     .asciz "Enter operation (+,-,*,/): "
msg_num2:   .asciz "Enter second number: "
msg_result: .asciz " = "
msg_newline:.asciz "\n"
msg_div0:   .asciz "Error: division by zero\n"
msg_badop:  .asciz "Error: unknown operator\n"

state:      .word 0
num1:       .word 0
num2:       .word 0
op:         .byte 0
done_flag:  .word 0

input_buf:  .space 64
input_len:  .word 0

.section .text
.globl _start
_start:
    PUSH ra
    PUSH a0              # a0 = shell's mtvec

    bios_call bios_clin

    la   t0, trap_handler
    csrw mtvec, t0

    li   t1, 8
    csrs mstatus, t1
    li   t1, 1 << 11
    csrs mie, t1

    li s0, 0
    li s1, 0

    la   s0, input_buf
    la   s1, input_len
    sw   zero, 0(s1)

    la   t0, state
    sw   zero, 0(t0)
    la   t0, num1
    sw   zero, 0(t0)
    la   t0, num2
    sw   zero, 0(t0)
    la   t0, op
    sb   zero, 0(t0)
    la   t0, done_flag
    sw   zero, 0(t0)

    la   a0, msg_num1
    bios_call bios_puts

loop:
    wfi
    # check done flag after every interrupt
    la   t0, done_flag
    lw   t0, 0(t0)
    bnez t0, exit_program
    j    loop

# ── Exit: called from loop, NOT from trap handler ────────────────────────
exit_program:
    li   t0, 8
    csrc mstatus, t0           # disable interrupts

    POP  t0                    # shell's mtvec
    csrw mtvec, t0
    POP  ra
    ret                        # clean return to shell

# ── Trap handler ─────────────────────────────────────────────────────────
.align 4
trap_handler:
    PUSH ra; PUSH s0; PUSH s1; PUSH s2

    la   s0, input_buf
    la   s1, input_len
    lw   s2, 0(s1)

    bios_call bios_getc

    li   t0, 10
    beq  a0, t0, enter_pressed
    li   t0, 8
    beq  a0, t0, backspace_pressed

    bios_call bios_putc

    li   t0, 63
    bge  s2, t0, trap_done

    add  t1, s0, s2
    sb   a0, 0(t1)
    addi s2, s2, 1
    j    trap_done

backspace_pressed:
    beqz s2, trap_done
    addi s2, s2, -1
    li   a0, 8
    bios_call bios_putc
    li   a0, ' '
    bios_call bios_putc
    li   a0, 8
    bios_call bios_putc
    j    trap_done

enter_pressed:
    add  t0, s0, s2
    sb   zero, 0(t0)
    beqz s2, clear_input

    la   a0, msg_newline
    bios_call bios_puts

    lw   t0, state
    li   t1, 0
    beq  t0, t1, got_num1
    li   t1, 1
    beq  t0, t1, got_op
    li   t1, 2
    beq  t0, t1, got_num2
    j    clear_input

got_num1:
    la   a0, input_buf
    bios_call bios_atoi
    la   t0, num1
    sw   a0, 0(t0)

    la   t0, state
    li   t1, 1
    sw   t1, 0(t0)

    la   a0, msg_op
    bios_call bios_puts
    j    clear_input

got_op:
    la   t0, input_buf
    lbu  t0, 0(t0)
    la   t1, op
    sb   t0, 0(t1)

    la   t0, state
    li   t1, 2
    sw   t1, 0(t0)

    la   a0, msg_num2
    bios_call bios_puts
    j    clear_input

got_num2:
    la   a0, input_buf
    bios_call bios_atoi
    la   t0, num2
    sw   a0, 0(t0)

    la   t0, num1
    lw   t1, 0(t0)         # t1 = num1
    la   t0, num2
    lw   t2, 0(t0)         # t2 = num2
    la   t0, op
    lbu  t0, 0(t0)         # t0 = operator

    li   t3, '+'
    beq  t0, t3, do_add
    li   t3, '-'
    beq  t0, t3, do_sub
    li   t3, '*'
    beq  t0, t3, do_mul
    li   t3, '/'
    beq  t0, t3, do_div

    la   a0, msg_badop
    bios_call bios_puts
    j    clear_input

do_add: add  t3, t1, t2
        j    print_result
do_sub: sub  t3, t1, t2
        j    print_result
do_mul: mul  t3, t1, t2
        j    print_result
do_div:
    beqz t2, div_by_zero
    div  t3, t1, t2
    j    print_result

div_by_zero:
    la   a0, msg_div0
    bios_call bios_puts
    j    set_done

print_result:
    mv   s0, t1    # num1
    mv   s1, t2    # num2
    mv   s2, t3    # result

    mv   a0, s0
    bios_call bios_puti
    li   a0, ' '
    bios_call bios_putc
    la   t0, op
    lbu  a0, 0(t0)
    bios_call bios_putc
    li   a0, ' '
    bios_call bios_putc
    mv   a0, s1
    bios_call bios_puti
    la   a0, msg_result
    bios_call bios_puts
    mv   a0, s2
    bios_call bios_puti
    la   a0, msg_newline
    bios_call bios_puts

set_done:
    # set done flag — loop will see it after mret and call exit_program
    la   t0, done_flag
    li   t1, 1
    sw   t1, 0(t0)
    j    clear_input

clear_input:
    sw   zero, 0(s1)
    li   s2, 0
    j    trap_done_no_save

trap_done:
    sw   s2, 0(s1)
trap_done_no_save:
    bios_call bios_clin

    POP s2; POP s1; POP s0; POP ra

    li   t0, 11
    csrrc zero, mip, t0
    mret                       # ← always mret, never ret from trap
