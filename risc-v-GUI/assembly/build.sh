#!/usr/bin/env bash
set -euo pipefail

TOOLS="../tools/linux/bin"
AS="$TOOLS/riscv-none-elf-as"
LD="$TOOLS/riscv-none-elf-ld"
OC="$TOOLS/riscv-none-elf-objcopy"
NM="$TOOLS/riscv-none-elf-nm"
FLAGS="-march=rv32imazicsr -g"
ASM="."

# ── Clean ────────────────────────────────────────────────────────────────
if [[ "${1:-}" == "clean" ]]; then
    rm -f \
        "$ASM/bios_rom.o"    "$ASM/bios_rom.elf"  "$ASM/bios_rom.bin" \
        "$ASM/bios_symbols.s" \
        "$ASM/program.o"     "$ASM/program.elf" \
        "$ASM/calc.o"        "$ASM/calc.elf"      "$ASM/calc.bin"
    echo "Clean done."
    exit 0
fi

# ── BIOS ────────────────────────────────────────────────────────────────
echo "[1/8] Assembling bios_rom.s..."
"$AS" $FLAGS -o "$ASM/bios_rom.o" "$ASM/bios_rom.s"

echo "[2/8] Linking bios_rom.elf..."
"$LD" --section-start=.text=0x0 -o "$ASM/bios_rom.elf" "$ASM/bios_rom.o"

echo "[3/8] Generating bios_rom.bin..."
"$OC" -O binary "$ASM/bios_rom.elf" "$ASM/bios_rom.bin"

echo "[4/8] Generating bios_symbols.s..."
"$NM" --format=posix "$ASM/bios_rom.elf" \
    | awk '{ print ".equ " $1 ", 0x" $3 }' \
    > "$ASM/bios_symbols.s"

# ── Program ─────────────────────────────────────────────────────────────
echo "[5/8] Assembling shell.s..."
"$AS" $FLAGS -o "$ASM/program.o" "$ASM/shell.s"

echo "[6/8] Linking program.elf..."
"$LD" -T "$ASM/linker.ld" "$ASM/program.o" -o "$ASM/program.elf"

# ── Calc ────────────────────────────────────────────────────────────────
echo "[7/8] Assembling calc.s..."
"$AS" $FLAGS -o "$ASM/calc.o" "$ASM/calc.s"

echo "[8/8] Linking and copying calc..."
"$LD" --no-relax -o "$ASM/calc.elf" "$ASM/calc.o"
"$OC" -O binary "$ASM/calc.elf" "$ASM/calc.bin"

echo "Build complete: bios_rom.bin, program.elf, calc.bin"