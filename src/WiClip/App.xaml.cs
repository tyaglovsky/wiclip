using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace WiClip;

public partial class App : Application
{
    private const string MutexName = @"Local\WiClip.SingleInstance";
    private const string ShowEventName = @"Local\WiClip.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;

    private AppSettings _settings = null!;
    private HistoryStore _store = null!;
    private MessageWindow _msgWindow = null!;
    private ClipboardMonitor _monitor = null!;
    private HotKeyManager _hotKeys = null!;
    private NotifyIcon _tray = null!;
    private HistoryWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Start(e);
        }
        catch (Exception ex)
        {
            // Исключение здесь означает, что приложение вообще не поднялось:
            // DispatcherUnhandledException до старта цикла сообщений ещё не работает.
            Log.Error($"Startup failed: {ex}");
            System.Windows.MessageBox.Show(
                $"WiClip failed to start:\n\n{ex.Message}\n\nDetails: %APPDATA%\\WiClip\\wiclip.log",
                "WiClip", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void Start(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var isFirst);
        if (!isFirst)
        {
            // Второй запуск просто показывает окно уже работающего экземпляра.
            try
            {
                EventWaitHandle.OpenExisting(ShowEventName).Set();
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not signal the running instance: {ex.Message}");
            }
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error($"Unhandled error: {args.Exception}");
            args.Handled = true;
        };

        _settings = AppSettings.Load();
        Localization.Apply(_settings.Language);
        Theme.Apply(_settings.Theme);

        if (AppSettings.FirstRun)
        {
            // После установки логично запускаться вместе с системой; выключается в настройках.
            // Если запись уже сделал установщик (в том числе для всех пользователей) — не дублируем.
            if (!Autostart.IsMachineWide && !Autostart.IsEnabled) Autostart.Set(true);
            _settings.Save();
        }

        _store = new HistoryStore(_settings);

        _msgWindow = new MessageWindow();
        _msgWindow.MessageReceived += OnMessage;

        _monitor = new ClipboardMonitor(_msgWindow.Handle, _store, _settings);

        _hotKeys = new HotKeyManager(_msgWindow.Handle);
        _hotKeys.Pressed += ShowHistoryWindow;
        var hotKeyError = _hotKeys.Register(_settings.HotKey);

        SetupTray();
        SetupShowEventListener();

        if (hotKeyError is not null)
        {
            Log.Warn(hotKeyError);
            _tray.ShowBalloonTip(7000, "WiClip", hotKeyError, ToolTipIcon.Warning);
        }
        else if (e.Args.Contains("--autostart", StringComparer.OrdinalIgnoreCase))
        {
            _tray.ShowBalloonTip(4000, "WiClip",
                Strings.Format("BalloonStarted", _settings.HotKey), ToolTipIcon.Info);
        }

        Log.Info("WiClip started.");

        if (!e.Args.Contains("--autostart", StringComparer.OrdinalIgnoreCase) &&
            !e.Args.Contains("--silent", StringComparer.OrdinalIgnoreCase))
        {
            ShowHistoryWindow();
        }
    }

    private bool OnMessage(int msg, IntPtr wParam, IntPtr lParam) =>
        _hotKeys.HandleMessage(msg, wParam) || _monitor.HandleMessage(msg);

    private void SetupShowEventListener()
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var thread = new Thread(() =>
        {
            while (_showEvent!.WaitOne())
                Dispatcher.Invoke(ShowHistoryWindow);
        })
        { IsBackground = true, Name = "WiClip.ShowListener" };
        thread.Start();
    }

    private void SetupTray()
    {
        _tray = new NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = Strings.Format("TrayTooltip", _settings.HotKey)
        };

        _tray.DoubleClick += (_, _) => ShowHistoryWindow();
        RebuildTrayMenu();
    }

    /// <summary>Меню трея пересобирается при смене языка.</summary>
    private void RebuildTrayMenu()
    {
        _tray.ContextMenuStrip?.Dispose();

        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.Format("MenuOpen", _settings.HotKey), null, (_, _) => ShowHistoryWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.MenuClear, null, (_, _) => ClearHistory());
        menu.Items.Add(Strings.MenuSettings, null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.MenuExit, null, (_, _) => ExitApp());
        _tray.ContextMenuStrip = menu;
    }

    private static Icon LoadIcon()
    {
        try
        {
            var stream = GetResourceStream(new Uri("Assets/wiclip.ico", UriKind.Relative))?.Stream;
            if (stream is not null) return new Icon(stream);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not load the tray icon: {ex.Message}");
        }
        return SystemIcons.Application;
    }

    private void ShowHistoryWindow()
    {
        // Окно, которое было активно до вызова — туда потом вставляем.
        var target = Native.GetForegroundWindow();

        if (_window is null)
        {
            _window = new HistoryWindow(_store, _settings, _monitor);
            _window.SettingsApplied += ApplySettings;
        }

        _window.ShowFor(target);
    }

    private void ClearHistory()
    {
        var result = System.Windows.MessageBox.Show(
            Strings.ClearConfirm,
            "WiClip", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes) _store.Clear();
    }

    private void ShowSettings()
    {
        var dlg = new SettingsWindow(_settings);
        if (dlg.ShowDialog() != true) return;

        _settings.Save();
        Localization.Apply(_settings.Language);
        Theme.Apply(_settings.Theme);
        ApplySettings();
        RebuildTrayMenu();

        // Строки в XAML читаются один раз при загрузке окна — пересоздаём его,
        // иначе смена языка не была бы видна до перезапуска.
        _window?.CloseForReal();
        _window = null;
    }

    /// <summary>Применить настройки, которые влияют не только на внешний вид.</summary>
    private void ApplySettings()
    {
        var error = _hotKeys.Register(_settings.HotKey);
        if (error is not null)
            _tray.ShowBalloonTip(7000, "WiClip", error, ToolTipIcon.Warning);

        _tray.Text = Strings.Format("TrayTooltip", _settings.HotKey);

        if (!_settings.PersistHistory) HistoryStore.PurgeDisk();
        else _store.Save();
    }

    private void ExitApp()
    {
        Log.Info("WiClip is shutting down.");
        _window?.CloseForReal();
        _window = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _store?.Save();
        _hotKeys?.Dispose();
        _monitor?.Dispose();
        _msgWindow?.Dispose();

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _showEvent?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
