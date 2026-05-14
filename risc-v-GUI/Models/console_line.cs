namespace risc_v_GUI.Models;

public record ConsoleLine
{
    public string Text  { get; init; } = "";
    public string Color { get; init; } = "#4c4f69";
}