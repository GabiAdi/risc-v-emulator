using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using risc_v_GUI.ViewModels;

namespace risc_v_GUI;

public partial class MemoryView : Window
{
    public MemoryView()
    {
        InitializeComponent();
    }

    private uint current_addr = 0;
    private uint current_range = 32;
    
    private async void refresh(object? sender, RoutedEventArgs e)
    {
        if(DataContext is not MainWindowViewModel viewModel) return;
        
        current_range = uint.Parse(tb_entry.Text);
        
        await viewModel.update_memory_view(current_addr, 0, current_range);
    }
    
    private async void goto_addr(object? sender, RoutedEventArgs e)
    {
        if(DataContext is not MainWindowViewModel viewModel) return;
        
        current_addr = uint.Parse(tb_address.Text, System.Globalization.NumberStyles.HexNumber);
        current_range = uint.Parse(tb_entry.Text);
        
        await viewModel.update_memory_view(current_addr, 0, current_range);
    }

    private void search(object? sender, RoutedEventArgs e)
    {
        
    }
}