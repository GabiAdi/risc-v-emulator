using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using risc_v_GUI.Services;
using risc_v_GUI.ViewModels;
using risc_v;
using IODevice = risc_v.IODevice;

namespace risc_v_GUI;

public partial class DeviceView : Window
{
    private readonly ObservableCollection<DeviceViewModel> _devices = new();

    public DeviceView(List<IMemoryDevice> devices)
    {
        InitializeComponent();
        TableList.ItemsSource = _devices;
        foreach (IMemoryDevice device in devices)
        {
            var vm = new DeviceViewModel()
            {
                Type = device switch
                {
                    Rom => "ROM",
                    Ram => "RAM",
                    Disk => "Disk",
                    Gpu => "GPU",
                    risc_v_GUI.Services.IODevice => "Console",
                    _ => "Unkown",
                },
                StartAddress = device.start_addr,
                Size = device.size,
                FilePath = device switch
                {
                    Disk disk => disk.file_path,
                    Rom rom => rom.file_path,
                    _ => "",
                },
            };
            vm.ApplyFixedSize();
            _devices.Add(vm);
        }
    }
    
    private async void OnSourceItemPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: string deviceType }) return;

        var data = new DataObject();
        data.Set("new-device", deviceType);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
    }

    // ── Row: start reorder drag (skip if clicking a TextBox or Button) ───
    private async void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is TextBox or Button) return;
        if (sender is not Control { DataContext: DeviceViewModel device }) return;

        var data = new DataObject();
        data.Set("row-index", _devices.IndexOf(device).ToString());
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
    }

    // ── DragOver ─────────────────────────────────────────────────────────
    private void OnTableDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains("new-device") || e.Data.Contains("row-index")
            ? DragDropEffects.Copy | DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    // ── Drop ─────────────────────────────────────────────────────────────
    private void OnTableDrop(object? sender, DragEventArgs e)
    {
        int insertAt = GetInsertIndex(e);

        if (e.Data.Contains("new-device"))
        {
            var type = e.Data.Get("new-device") as string ?? "";
            var vm = new DeviceViewModel {Type = type};
            vm.ApplyFixedSize();
            _devices.Insert(insertAt, vm);
        }
        else if (e.Data.Contains("row-index"))
        {
            if (!int.TryParse(e.Data.Get("row-index") as string, out int from)) return;
            if (from == insertAt) return;

            var item = _devices[from];
            _devices.RemoveAt(from);

            int to = Math.Clamp(insertAt > from ? insertAt - 1 : insertAt, 0, _devices.Count);
            _devices.Insert(to, item);
        }
    }

    // ── Browse (ROM / Disk) ──────────────────────────────────────────────
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: DeviceViewModel device }) return;

        var (title, patterns) = device.Type switch
        {
            "ROM"  => ("Select ROM binary",  new[] { "*.bin", "*.rom" }),
            "Disk" => ("Select disk image",  new[] { "*.img" }),
            _      => ("Select file",        new[] { "*.*" })
        };

        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title         = title,
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(AppDomain.CurrentDomain.BaseDirectory)),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Supported files") { Patterns = patterns },
                new FilePickerFileType("All files")       { Patterns = ["*.*"] }
            ]
        });

        if (result.Count > 0)
            device.FilePath = result[0].Path.LocalPath;
    }

    // ── Remove ───────────────────────────────────────────────────────────
    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: DeviceViewModel device }) return;
        _devices.Remove(device);
    }

    // ── Insert position via hit-testing ──────────────────────────────────
    private int GetInsertIndex(DragEventArgs e)
    {
        for (int i = 0; i < _devices.Count; i++)
        {
            if (TableList.ContainerFromIndex(i) is not Control row) continue;
            if (e.GetPosition(row).Y < row.Bounds.Height / 2)
                return i;
        }
        return _devices.Count;
    }

    private void OnSaveClick(Object? sender, RoutedEventArgs e)
    {
        if(DataContext is not MainWindowViewModel viewModel) return;

        if (sender is Button b1)
            b1.IsEnabled = false;
        try
        {
            viewModel.new_devices(_devices);
        }
        catch (Exception ex)
        {
            MessageBoxService.ShowError(ex.Message, this);
        }
        finally
        {
            if(sender is Button b2) 
                b2.IsEnabled = true;
        }
    }
}

