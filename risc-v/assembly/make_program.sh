riscv32-linux-gnu-as -o program.o program.s
riscv32-linux-gnu-ld -T linker.ld -o program program.o # Linker uses linker.ld to set absolute addresses