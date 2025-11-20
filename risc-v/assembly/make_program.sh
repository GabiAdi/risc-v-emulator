riscv32-linux-gnu-as -march=rv32ima -o program.o atomics.s
riscv32-linux-gnu-ld -T linker.ld -o program program.o # Linker uses linker.ld to set absolute addresses