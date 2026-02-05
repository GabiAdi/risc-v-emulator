using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using risc_v_GUI.Services;
using risc_v_GUI.ViewModels;
using risc_v;
using SystemHandler = risc_v_GUI.Services.SystemHandler;

namespace risc_v_GUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private async void halt(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        
        if (sender is Button b1)
            b1.IsEnabled = false;
        
        try
        {
            await viewModel.halt();
        }
        finally
        {
            if (sender is Button b2)
                b2.IsEnabled = true;
        }
    }

    private async void unhalt(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        
        if (sender is Button b1)
            b1.IsEnabled = false;
        
        try
        {
            await viewModel.unhalt();
        }
        finally
        {
            if (sender is Button b2)
                b2.IsEnabled = true;
        }
    }

    private async void run_until_break(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        
        if (sender is Button b1)
            b1.IsEnabled = false;
        
        try
        {
            await viewModel.run_until_break();
        }
        finally
        {
            if (sender is Button b2)
                b2.IsEnabled = true;
        }
    }

    private async void interrupt(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        
        if (sender is Button b1)
            b1.IsEnabled = false;
        
        try
        {
            await viewModel.interrupt();
        }
        finally
        {
            if (sender is Button b2)
                b2.IsEnabled = true;
        }
    }
    
    private async void clear_interrupt(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
        
        if (sender is Button b1)
            b1.IsEnabled = false;
        
        try
        {
            await viewModel.clear_interrupt();
        }
        finally
        {
            if (sender is Button b2)
                b2.IsEnabled = true;
        }
    }

    private async void step(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;
    
        if (sender is Button b1)
            b1.IsEnabled = false;
    
        try
        {
            await viewModel.StepAsync();
        }
        finally
        {
            if (sender is Button b2)
                b2.IsEnabled = true;
        }
    }

    private async void on_key_down(Object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel) return;

        await viewModel.key_pressed(e.Key);
    }
}