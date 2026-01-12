ENTRY(_start)

SECTIONS
{
    .text 0x0 : { *(.text) }
    .data 0x1200 : { *(.data) }
    .bss  0x2000 : { *(.bss) }
}
