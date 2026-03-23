using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using risc_v_GUI.Views;

namespace risc_v_GUI.Services;

public static class MessageBoxService
{
    public static void ShowError(string message, Visual visual)
    {
        MessageBox mb = new MessageBox(message)
        {
            DataContext = visual,
        };
        mb.Show();
    }
}