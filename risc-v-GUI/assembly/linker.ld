ENTRY(_start)

SECTIONS
{
    .text : {
        *(.text*)
    }

    . = 0x1000;
    .data : {
        *(.data*)
    }

    .bss : {
        *(.bss*) 
        . = ALIGN(4);  
    }
}
