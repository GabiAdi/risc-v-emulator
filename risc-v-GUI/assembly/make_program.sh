riscv32-linux-gnu-as -march=rv32imazicsr -g -o program.o gpu_test.s
riscv32-linux-gnu-ld -T linker.ld program.o -o program.elf