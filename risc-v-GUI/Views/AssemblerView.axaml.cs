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

namespace risc_v_GUI.Views;

public partial class AssemblerView : Window
{
    public AssemblerView()
    {
        InitializeComponent();
    }
}