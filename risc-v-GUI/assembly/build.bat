@echo off
setlocal enabledelayedexpansion

set TOOLS=..\tools\win\bin
set AS=%TOOLS%\riscv-none-elf-as.exe
set LD=%TOOLS%\riscv-none-elf-ld.exe
set OC=%TOOLS%\riscv-none-elf-objcopy.exe
set NM=%TOOLS%\riscv-none-elf-nm.exe
set FLAGS=-march=rv32imazicsr -g
set ASM=.

:: ── Clean ────────────────────────────────────────────────────────────────
if /i "%~1"=="clean" (
    for %%F in (
        "%ASM%\bios_rom.o"    "%ASM%\bios_rom.elf"  "%ASM%\bios_rom.bin"
        "%ASM%\bios_symbols.s"
        "%ASM%\program.o"     "%ASM%\program.elf"
        "%ASM%\calc.o"        "%ASM%\calc.elf"       "%ASM%\calc.bin"
    ) do if exist %%F del %%F
    echo Clean done.
    exit /b 0
)

:: ── BIOS ────────────────────────────────────────────────────────────────
echo [1/8] Assembling bios_rom.s...
"%AS%" %FLAGS% -o "%ASM%\bios_rom.o" "%ASM%\bios_rom.s"
if errorlevel 1 goto error

echo [2/8] Linking bios_rom.elf...
"%LD%" --section-start=.text=0x0 -o "%ASM%\bios_rom.elf" "%ASM%\bios_rom.o"
if errorlevel 1 goto error

echo [3/8] Generating bios_rom.bin...
"%OC%" -O binary "%ASM%\bios_rom.elf" "%ASM%\bios_rom.bin"
if errorlevel 1 goto error

echo [4/8] Generating bios_symbols.s...
"%NM%" --format=posix "%ASM%\bios_rom.elf" > "%TEMP%\nm_output.txt"
if errorlevel 1 goto error

:: Use PowerShell to replicate the awk transformation
powershell -NoProfile -Command ^
    "Get-Content '%TEMP%\nm_output.txt' | ForEach-Object { $p = $_ -split '\s+'; if ($p.Count -ge 3) { '.equ ' + $p[0] + ', 0x' + $p[2] } }" ^
    > "%ASM%\bios_symbols.s"
if errorlevel 1 goto error

:: ── Program ─────────────────────────────────────────────────────────────
echo [5/8] Assembling shell.s...
"%AS%" %FLAGS% -o "%ASM%\program.o" "%ASM%\shell.s"
if errorlevel 1 goto error

echo [6/8] Linking program.elf...
"%LD%" -T "%ASM%\linker.ld" "%ASM%\program.o" -o "%ASM%\program.elf"
if errorlevel 1 goto error

:: ── Calc ────────────────────────────────────────────────────────────────
echo [7/8] Assembling calc.s...
"%AS%" %FLAGS% -o "%ASM%\calc.o" "%ASM%\calc.s"
if errorlevel 1 goto error

echo [8/8] Linking and copying calc...
"%LD%" --no-relax -o "%ASM%\calc.elf" "%ASM%\calc.o"
if errorlevel 1 goto error
"%OC%" -O binary "%ASM%\calc.elf" "%ASM%\calc.bin"
if errorlevel 1 goto error

echo Build complete: bios_rom.bin, program.elf, calc.bin
exit /b 0

:error
echo Build failed. See error above.
exit /b 1