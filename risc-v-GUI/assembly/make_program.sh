riscv32-linux-gnu-as -march=rv32imazicsr -o program.o interrupt_test.s
riscv32-linux-gnu-ld -T linker.ld -o program program.o # Linker uses linker.ld to set absolute addresses