using System;
using System.Linq;
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

    private async void search(object? sender, RoutedEventArgs e)
    {
        if(DataContext is not MainWindowViewModel viewModel) return;
        if(tb_search.Text.Length == 0) return;
        
        bt_search.IsEnabled = false;

        string input = tb_search.Text;

        byte[] search_bytes;

        if (cb_search.SelectedItem == "String")
        {
            search_bytes = System.Text.Encoding.ASCII.GetBytes(input);
        }
        else if (cb_search.SelectedItem == "Binary")
        {
            search_bytes = Enumerable.Range(0, (input.Length + 7) / 8)
                .Select(i => Convert.ToByte(input.PadLeft(((input.Length + 7) / 8) * 8, '0')
                .Substring(i * 8, 8), 2))
                .ToArray();
            Array.Reverse(search_bytes);
        }
        else if (cb_search.SelectedItem == "Decimal")
        {
            search_bytes = BitConverter.GetBytes(int.Parse(input));
        }
        else
        {
            search_bytes = Convert.FromHexString(input);
            Array.Reverse(search_bytes);
        }
        
        await viewModel.search_memory(search_bytes);
        
        bt_search.IsEnabled = true;
    }
}