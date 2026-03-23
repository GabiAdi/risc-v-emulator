using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace risc_v_GUI.Views;

public partial class MessageBox : Window
{
    public MessageBox(string error)
    {
        InitializeComponent();
        
        MessageText.Text = error;
    }
    
    public void OnOkClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}