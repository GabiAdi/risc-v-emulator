using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using risc_v_GUI.Models;
using risc_v_GUI.Services;

namespace risc_v_GUI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private string _outputText = "";

        public string OutputText
        {
            get => _outputText;
            set
            {
                _outputText = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<string> Status { get; } = new ObservableCollection<string>();
        public ObservableCollection<MemoryRow> Memory { get; } = new ObservableCollection<MemoryRow>();
        public ObservableCollection<RegisterRow> Registers { get; } = new ObservableCollection<RegisterRow>();

        public EmulatorService Emulator { get; }

        public MainWindowViewModel(EmulatorService emulator)
        {
            Emulator = emulator ?? throw new System.ArgumentNullException(nameof(emulator));
            
            UpdateMemoryView();
            UpdateRegistersView();

            Emulator.system_handler.OutputProduced += OnOutputProduced;
            Emulator.system_handler.StatusChanged += OnStatusChanged;
            Emulator.devices.OfType<IODevice>().First().OutputWritten += io_written;
        }
        
        public async Task halt()
        {
            await Task.Run(() =>
            {
                Emulator.cpu.halt();
            });
        }
        
        public async Task unhalt()
        {
            await Task.Run(() =>
            {
                Emulator.cpu.resume();
            });
        }

        public async Task StepAsync()
        {
            await Task.Run(() =>
            {
                Emulator.cpu.step();
            });
            UpdateMemoryView();
            UpdateRegistersView();
        }
        
        public async Task interrupt()
        {
            await Task.Run(() =>
            {
                Emulator.cpu.external_interrupt();
            });
        }
        
        public async Task clear_interrupt()
        {
            await Task.Run(() =>
            {
                Emulator.cpu.clear_external_interrupt();
            });
        }
        
        public async Task run_until_break()
        {
            await Task.Run(() =>
            {
                Emulator.cpu.run_until_halt();
                Emulator.cpu.resume();
            });
            UpdateMemoryView();
            UpdateRegistersView();
        }
        
        private void io_written(string output)
        {
            Dispatcher.UIThread.Post(() => OutputText += output);
        }
        
        private void OnOutputProduced(string output)
        {
            Dispatcher.UIThread.Post(() => Status.Add(output));
        }

        private void OnStatusChanged(string status)
        {
            Dispatcher.UIThread.Post(() => Status.Add(status));
        }
        
        private string GetAsciiRepresentation(uint value)
        {
            char[] chars = new char[4];
            for (int i = 0; i < 4; i++)
            {
                byte b = (byte)((value >> (i * 8)) & 0xFF);
                chars[i] = (b >= 32 && b <= 126) ? (char)b : '.';
            }
            return new string(chars);
        }
        
        private string GetRegisterPointer(uint address)
        {
            string registers_string = "";

            uint[] regs = Emulator.cpu.get_registers();

            if (Emulator.cpu.get_pc() == address)
            {
                registers_string += "PC, ";
            }
            
            for(int i=0; i<regs.Length; i++)
            {
                if (regs[i] == address || regs[i] - 1 == address || regs[i] - 2 == address || regs[i] - 3 == address)
                {
                    registers_string += $"x{i}, ";
                }
            }

            if (registers_string.Length > 2)
            {
                registers_string = registers_string.Substring(0, registers_string.Length - 2);
            }
            
            return registers_string;
        }

        private void UpdateRegistersView()
        {
            Registers.Clear();
            uint[] regs = Emulator.cpu.get_registers();
            Registers.Add(new RegisterRow()
            {
                Register = "PC",
                Value = Emulator.cpu.get_pc().ToString("X8"),
            });
            for (int i = 0; i < regs.Length; i++)
            {
                Registers.Add(new RegisterRow()
                {
                    Register = $"x{i}",
                    Value = regs[i].ToString("X8"),
                });
            }
        }
        
        private void UpdateMemoryView()
        {
            uint x=32;
            if (Emulator.cpu.get_pc() < 32)
            {
                x = Emulator.cpu.get_pc();
            }
            Memory.Clear();
            for(uint i=Emulator.cpu.get_pc()-x; i<Emulator.cpu.get_pc()+(64-x); i+=4)
            {
                uint value = Emulator.memory.read_word(i);
                Memory.Add(new MemoryRow()
                {
                    Address = (i).ToString("X8"),
                    Value = value.ToString("X8"),
                    Ascii = GetAsciiRepresentation(value),
                    Disassembly = Emulator.disassembler.DisassembleInstruction(value, i),
                    Register = GetRegisterPointer(i),
                });                
            }
        }
    }
}