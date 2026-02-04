namespace risc_v;

public interface IInterruptDevice
{
    public event Action? interrupt_requested;
    public event Action? interrupt_cleared;
}