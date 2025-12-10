riscv32-linux-gnu-as -march=rv32ima -o program.o hello_world.s
riscv32-linux-gnu-ld -T linker.ld -o program program.o # Linker uses linker.ld to set absolute addresses