ENTRY(_start)

SECTIONS
{
    . = 0x4;
    
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
