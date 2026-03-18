using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using risc_v;

namespace risc_v_GUI.ViewModels;

public class DeviceViewModel : INotifyPropertyChanged
{
    private uint  _startAddress;
    private string _filePath = "";
    private uint  _size     = 0x10000;

    public required string Type { get; init; }

    public string BadgeColor => Type switch
    {
        "ROM"     => "#f38ba8",
        "RAM"     => "#a6e3a1",
        "Disk"    => "#fab387",
        "GPU"     => "#cba6f7",
        "Console" => "#89dceb",
        _         => "#6c7086"
    };

    public bool HasFile      => Type is "ROM" or "Disk";
    public bool IsFixedSize  => Type is "Disk" or "GPU" or "Console";
    
    public void ApplyFixedSize()
    {
        if (!IsFixedSize) return;
        _size = Type switch
        {
            "Disk"    => Disk.default_size,
            "GPU"     => Gpu.default_size,
            "Console" => risc_v_GUI.Services.IODevice.default_size,
            _         => _size,
        };
    }

    // ── Start address ────────────────────────────────────────────────────
    public uint StartAddress
    {
        get => _startAddress;
        set { _startAddress = value; OnPropertyChanged(); OnPropertyChanged(nameof(StartAddressText)); }
    }

    public string StartAddressText
    {
        get => $"0x{StartAddress:X8}";
        set
        {
            var s = value.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
            if (uint.TryParse(s, NumberStyles.HexNumber, null, out var r))
                StartAddress = r;
        }
    }

    // ── File path (ROM / Disk) ───────────────────────────────────────────
    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileName)); }
    }

    public string FileName =>
        string.IsNullOrEmpty(_filePath) ? "No file chosen" : Path.GetFileName(_filePath);

    // ── Size (RAM) ───────────────────────────────────────────────────────
    public uint Size
    {
        get => _size;
        set { _size = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeText)); }
    }

    public string SizeText
    {
        get => $"0x{Size:X}";
        set
        {
            var s = value.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
            if (uint.TryParse(s, NumberStyles.HexNumber, null, out var r))
                Size = r;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}