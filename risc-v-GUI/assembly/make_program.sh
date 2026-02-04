riscv32-linux-gnu-as -march=rv32imazicsr -o bios.o bios.s
riscv32-linux-gnu-as -march=rv32imazicsr -o program.o interrupt_test.s
riscv32-linux-gnu-ld -T linker.ld bios.o program.o -o program