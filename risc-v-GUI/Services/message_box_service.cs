using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace risc_v_GUI.Services;

public static class MessageBoxService
{
    public static void ShowError(string message, Visual visual)
    {
        var error_win = new Window
        {
            Title = "Error",
            Width = 300,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(10),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "An Error Occured", FontWeight = FontWeight.Bold },
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = HorizontalAlignment.Center,
                    }
                }
            }
        };
                    
        ((Button)((StackPanel)error_win.Content).Children[2]).Click += (s, args) => error_win.Close();
        error_win.ShowDialog(TopLevel.GetTopLevel(visual) as Window);
    }
}