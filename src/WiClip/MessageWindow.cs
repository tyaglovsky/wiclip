using System.Windows.Interop;

namespace WiClip;

/// <summary>
/// Невидимое окно — приёмник сообщений Windows (буфер обмена и горячие клавиши).
/// Живёт всё время работы приложения, в трее.
/// </summary>
internal sealed class MessageWindow : IDisposable
{
    private readonly HwndSource _source;

    public IntPtr Handle => _source.Handle;

    /// <summary>Возвращает true, если сообщение обработано.</summary>
    public event Func<int, IntPtr, IntPtr, bool>? MessageReceived;

    public MessageWindow()
    {
        var parameters = new HwndSourceParameters("WiClip.MessageWindow")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0x800000 // WS_BORDER, окно не показывается
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (MessageReceived?.Invoke(msg, wParam, lParam) == true)
            handled = true;

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
