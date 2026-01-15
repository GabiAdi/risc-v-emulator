using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
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

    private async void step(Object? sender, RoutedEventArgs e)
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
}