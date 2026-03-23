using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using risc_v_GUI.Services;
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

        try
        {
            current_range = uint.Parse(tb_entry.Text);
        }
        catch (FormatException)
        {
            MessageBoxService.ShowError("Invalid range format", this);
            return;
        }
        
        await viewModel.update_memory_view(current_addr, 0, current_range);
    }
    
    private async void goto_addr(object? sender, RoutedEventArgs e)
    {
        if(DataContext is not MainWindowViewModel viewModel) return;

        try
        {
            if(tb_address.Text.Substring(0, 2) == "0x") current_addr = uint.Parse(tb_address.Text.Substring(2, tb_address.Text.Length-2), System.Globalization.NumberStyles.HexNumber);
            else current_addr = uint.Parse(tb_address.Text, System.Globalization.NumberStyles.HexNumber);
        }
        catch (FormatException)
        {
            MessageBoxService.ShowError("Invalid address format", this);
            return;
        }
        try
        {
            current_range = uint.Parse(tb_entry.Text);
        }
        catch (FormatException)
        {
            MessageBoxService.ShowError("Invalid range format", this);
            return;
        }
        
        await viewModel.update_memory_view(current_addr, 0, current_range);
    }

    private async void search(object? sender, RoutedEventArgs e)
    {
        if(DataContext is not MainWindowViewModel viewModel) return;
        if(tb_search.Text != null && tb_search.Text.Length == 0) return;
        
        bt_search.IsEnabled = false;

        string input = tb_search.Text;

        byte[] search_bytes;

        if (cb_search.SelectedItem == "String")
        {
            try
            {
                search_bytes = System.Text.Encoding.ASCII.GetBytes(input);
            }
            catch
            {
                MessageBoxService.ShowError("Invalid search string", this);
                bt_search.IsEnabled = true;
                return;
            }
        }
        else if (cb_search.SelectedItem == "Binary")
        {
            try
            {
                search_bytes = Enumerable.Range(0, (input.Length+7) / 8)
                    .Select(i => Convert.ToByte(input.PadLeft(((input.Length+7) / 8) * 8, '0')
                        .Substring(i * 8, 8), 2))
                    .ToArray();
                Array.Reverse(search_bytes);
            }
            catch (FormatException)
            {
                MessageBoxService.ShowError("Invalid binary format", this);
                bt_search.IsEnabled = true;
                return;
            }
        }
        else if (cb_search.SelectedItem == "Decimal")
        {
            try
            {
                search_bytes = BitConverter.GetBytes(uint.Parse(input));
            }
            catch (FormatException)
            {
                MessageBoxService.ShowError("Invalid decimal format", this);
                bt_search.IsEnabled = true;
                return;
            }
        }
        else
        {
            try
            {
                search_bytes = Convert.FromHexString(input);
                Array.Reverse(search_bytes);
            }
            catch (FormatException)
            {
                MessageBoxService.ShowError("Invalid hex format (must be 8 digits)", this);
            bt_search.IsEnabled = true;
                return;
            }
        }
        
        await viewModel.search_memory(search_bytes);
        
        bt_search.IsEnabled = true;
    }
}