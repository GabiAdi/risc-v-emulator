riscv32-linux-gnu-as -march=rv32imazicsr -g -o bios.o bios.s
riscv32-linux-gnu-as -march=rv32imazicsr -g -o program.o disk_test.s
riscv32-linux-gnu-ld -T linker.ld bios.o program.o -o program.elf