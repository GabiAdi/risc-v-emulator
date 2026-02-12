using System;
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

        byte[] search_bytes;
        
        if (tb_search.Text.Length > 2 && tb_search.Text.Substring(0, 2) == "0x")
        {
            search_bytes = Convert.FromHexString(tb_search.Text.Substring(2, tb_search.Text.Length-2));
            Array.Reverse(search_bytes);
        } else
        {
            search_bytes = System.Text.Encoding.ASCII.GetBytes(tb_search.Text);
        }
        
        await viewModel.search_memory(search_bytes);
        
        bt_search.IsEnabled = true;
    }
}