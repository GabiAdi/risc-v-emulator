using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using risc_v_GUI.Services;

namespace risc_v_GUI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<string> Output { get; } = new ObservableCollection<string>();

        public EmulatorService Emulator { get; }

        public MainWindowViewModel(EmulatorService emulator)
        {
            Emulator = emulator ?? throw new System.ArgumentNullException(nameof(emulator));

            Emulator.system_handler.OutputProduced += OnOutputProduced;
            Emulator.system_handler.StatusChanged += OnStatusChanged;
        }

        public async Task StepAsync()
        {
            await Task.Run(() =>
            {
                Emulator.cpu.step();
            });
        }
        
        private void OnOutputProduced(string output)
        {
            Dispatcher.UIThread.Post(() => Output.Add(output));
        }

        private void OnStatusChanged(string status)
        {
            Dispatcher.UIThread.Post(() => Output.Add(status));
        }
    }
}