ARCH  = riscv32-linux-gnu
AS    = $(ARCH)-as
LD    = $(ARCH)-ld
OC    = $(ARCH)-objcopy
NM    = $(ARCH)-nm
FLAGS = -march=rv32imazicsr -g

all: bios_rom.bin bios_symbols.s program.elf

# ── BIOS ────────────────────────────────────────────────────────────────
bios_rom.o: bios_rom.s
	$(AS) $(FLAGS) -o bios_rom.o bios_rom.s

bios_rom.elf: bios_rom.o
	$(LD) --section-start=.text=0x0 -o bios_rom.elf bios_rom.o

bios_rom.bin: bios_rom.elf
	$(OC) -O binary bios_rom.elf bios_rom.bin

bios_symbols.s: bios_rom.elf
	$(NM) --format=posix bios_rom.elf | awk '{ print ".equ " $$1 ", 0x" $$3 }' > bios_symbols.s

# ── Program ─────────────────────────────────────────────────────────────
program.o: shell.s bios_symbols.s
	$(AS) $(FLAGS) -o program.o shell.s

program.elf: program.o linker.ld
	$(LD) -T linker.ld program.o -o program.elf

# ── Clean ────────────────────────────────────────────────────────────────
clean:
	rm -f bios_rom.o bios_rom.elf bios_rom.bin bios_symbols.s program.o program.elf