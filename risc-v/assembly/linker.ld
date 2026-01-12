ENTRY(_start)

SECTIONS
{
    .text 0x0 : { *(.text) }
    .data 0x12FF : { *(.data) }
    .bss  0x2000 : { *(.bss) }
}
