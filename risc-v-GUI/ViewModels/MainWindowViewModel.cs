using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using risc_v_GUI.Models;
using risc_v_GUI.Services;
using risc_v;
using IODevice = risc_v_GUI.Services.IODevice;

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
        
        private bool _halt_on_ebreak = true;

        public bool halt_on_ebreak
        {
            get => _halt_on_ebreak;
            set
            {
                _halt_on_ebreak = value;
                Emulator.cpu.halt_on_break = value;
                OnPropertyChanged();
            }
        }
        
        public ICommand toggle_halt_on_ebreak { get; }
        public ICommand enter_pressed { get; }
        public ICommand back_pressed { get; }
        
        public ObservableCollection<string> Status { get; } = new ObservableCollection<string>();
        public ObservableCollection<MemoryRow> Memory { get; } = new ObservableCollection<MemoryRow>();
        public ObservableCollection<RegisterRow> Registers { get; } = new ObservableCollection<RegisterRow>();

        public EmulatorService Emulator { get; }

        public event Action<char>? on_key_pressed;

        public MainWindowViewModel(EmulatorService emulator)
        {
            Emulator = emulator ?? throw new System.ArgumentNullException(nameof(emulator));
            
            UpdateMemoryView();
            UpdateRegistersView();

            Emulator.system_handler.OutputProduced += OnOutputProduced;
            Emulator.system_handler.StatusChanged += OnStatusChanged;
            Emulator.devices.OfType<IODevice>().First().OutputWritten += io_written;
            foreach (IODevice ioDevice in Emulator.devices.OfType<IODevice>())
            {
                on_key_pressed += ioDevice.key_pressed;
            }
            
            toggle_halt_on_ebreak = new RelayCommand(() =>
            {
                halt_on_ebreak = !halt_on_ebreak;
            });
            enter_pressed = new RelayCommand(() =>
            {
                on_key_pressed.Invoke('\n');
            });
            back_pressed = new RelayCommand(() =>
            {
                on_key_pressed.Invoke('\b');
            });
        }
        
        private char? KeyToChar(Key key) => key switch
        {
            >= Key.A and <= Key.Z => (char)('A' + (key - Key.A)),
            >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
            >= Key.NumPad0 and <= Key.NumPad9 => (char)('0' + (key - Key.NumPad0)),
            Key.Space => ' ',
            Key.Enter => '\n',
            Key.Tab => '\t',
            Key.Back => '\b',
            _ => null, // Not a printable character
        };

        public async Task key_pressed(Key key)
        {
            char? c = KeyToChar(key);
            if (c.HasValue)
                on_key_pressed.Invoke(c.Value);
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
            });
            UpdateMemoryView();
            UpdateRegistersView();
        }
        
        private void io_written(string output)
        {
            if (output == "\b" && OutputText.Length > 0)
            {
                Dispatcher.UIThread.Post(() => OutputText = OutputText.Substring(0, OutputText.Length - 1));
                return;
            }
            if(output != "\b")
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
                Name = "PC",
                Value = Emulator.cpu.get_pc().ToString("X8"),
            });
            for (int i = 0; i < regs.Length; i++)
            {
                Registers.Add(new RegisterRow()
                {
                    Register = $"x{i}",
                    Name = Enum.GetName(typeof(Cpu.register_names), i),
                    Value = regs[i].ToString("X8"),
                });
            }
            foreach (uint i in (uint[])Enum.GetValues(typeof(Cpu.CSR)))
            {
                Registers.Add(new RegisterRow()
                {
                    Register = i.ToString("X3"),
                    Name = Enum.GetName(typeof(Cpu.CSR), i),
                    Value = "0x" + Emulator.cpu.get_csr(i).ToString("X8"),
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