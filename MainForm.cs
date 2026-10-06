using System.Diagnostics;
using Microsoft.Win32;

namespace GamepadLauncher;

public class MainForm : Form
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string AppName = "GamepadLauncher";

    readonly Settings _s = Settings.Load();
    readonly bool _startHidden;
    bool _shownOnce, _exiting, _firstPoll = true, _wasConnected;
    DateTime _lastLaunch = DateTime.MinValue;

    readonly TextBox _path = new() { ReadOnly = true };
    readonly Button _browse = new() { Text = "Выбрать…" };
    readonly CheckBox _enabled = new() { Text = "Запускать игру, когда я включаю джойстик", AutoSize = true };
    readonly CheckBox _autostart = new() { Text = "Запускать программу вместе с Windows (будет в трее)", AutoSize = true };
    readonly CheckBox _already = new() { Text = "Запустить игру, если джойстик уже включён при старте программы", AutoSize = true };
    readonly NumericUpDown _delay = new() { Minimum = 0, Maximum = 120, Width = 60 };
    readonly Label _status = new() { AutoSize = true, Text = "Джойстик: проверяю…" };
    readonly Button _test = new() { Text = "Запустить игру сейчас (тест)", Width = 230, Height = 30 };
    readonly NotifyIcon _tray = new();
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };

    public MainForm(bool startHidden)
    {
        _startHidden = startHidden;

        Text = "Gamepad Launcher";
        ClientSize = new Size(540, 300);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        Icon = SystemIcons.Application;

        var l1 = new Label { Text = "Игра (exe файл):", AutoSize = true, Location = new Point(12, 14) };
        _path.SetBounds(12, 36, 410, 26);
        _browse.SetBounds(430, 34, 98, 30);
        _enabled.Location = new Point(12, 80);
        _autostart.Location = new Point(12, 108);
        _already.Location = new Point(12, 136);
        var l2 = new Label { Text = "Задержка перед запуском (сек):", AutoSize = true, Location = new Point(12, 170) };
        _delay.Location = new Point(220, 166);
        _status.Location = new Point(12, 204);
        _test.Location = new Point(12, 236);
        var hint = new Label
        {
            Text = "Закрытие окна сворачивает программу в трей. Выход — правый клик по значку в трее.",
            AutoSize = true, ForeColor = Color.Gray, Location = new Point(12, 276)
        };

        Controls.AddRange(new Control[] { l1, _path, _browse, _enabled, _autostart, _already, l2, _delay, _status, _test, hint });

        // загрузка настроек (до подключения обработчиков, чтобы не сохранять лишний раз)
        _path.Text = _s.GamePath;
        _enabled.Checked = _s.Enabled;
        _autostart.Checked = _s.StartWithWindows;
        _already.Checked = _s.LaunchIfAlreadyConnected;
        _delay.Value = Math.Clamp(_s.DelaySeconds, 0, 120);

        _browse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Выбери exe игры",
                Filter = "Программы (*.exe)|*.exe|Все файлы (*.*)|*.*"
            };
            if (!string.IsNullOrEmpty(_s.GamePath) && File.Exists(_s.GamePath))
                dlg.InitialDirectory = Path.GetDirectoryName(_s.GamePath);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _s.GamePath = dlg.FileName;
                _path.Text = dlg.FileName;
                _s.Save();
            }
        };
        _enabled.CheckedChanged += (_, _) => { _s.Enabled = _enabled.Checked; _s.Save(); };
        _autostart.CheckedChanged += (_, _) =>
        {
            _s.StartWithWindows = _autostart.Checked;
            SetAutostart(_autostart.Checked);
            _s.Save();
        };
        _already.CheckedChanged += (_, _) => { _s.LaunchIfAlreadyConnected = _already.Checked; _s.Save(); };
        _delay.ValueChanged += (_, _) => { _s.DelaySeconds = (int)_delay.Value; _s.Save(); };
        _test.Click += async (_, _) => await LaunchGameAsync(manual: true);

        // значок в трее
        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => ShowWindow());
        menu.Items.Add("Выход", null, (_, _) => ExitApp());
        _tray.Icon = SystemIcons.Application;
        _tray.Text = "Gamepad Launcher";
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowWindow();
        _tray.Visible = true;

        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    // ---------- опрос джойстика ----------

    void Poll()
    {
        var pads = Joy.GetConnected();
        bool connected = pads.Count > 0;
        _status.Text = connected
            ? "Джойстик: подключён (" + string.Join(", ", pads) + ")"
            : "Джойстик: не найден (включи его — игра запустится)";

        if (_firstPoll)
        {
            _firstPoll = false;
            _wasConnected = connected;
            if (connected && _s.Enabled && _s.LaunchIfAlreadyConnected)
                _ = LaunchGameAsync(manual: false);
            return;
        }

        // запуск только в момент, когда джойстик появился
        if (connected && !_wasConnected && _s.Enabled)
            _ = LaunchGameAsync(manual: false);

        _wasConnected = connected;
    }

    // ---------- запуск игры ----------

    async Task LaunchGameAsync(bool manual)
    {
        string path = _s.GamePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Notify("Не выбран exe игры или файл не найден.");
            return;
        }

        if (!manual)
        {
            if ((DateTime.Now - _lastLaunch).TotalSeconds < 30) return; // защита от двойного запуска
            if (IsRunning(path)) return;                                // игра уже запущена
            if (_s.DelaySeconds > 0) await Task.Delay(_s.DelaySeconds * 1000);
        }

        _lastLaunch = DateTime.Now;
        try
        {
            Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path)!
            });
        }
        catch (Exception ex)
        {
            Notify("Не удалось запустить игру: " + ex.Message);
        }
    }

    static bool IsRunning(string exePath)
    {
        var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exePath));
        bool running = procs.Length > 0;
        foreach (var p in procs) p.Dispose();
        return running;
    }

    void Notify(string text) => _tray.ShowBalloonTip(4000, "Gamepad Launcher", text, ToolTipIcon.Info);

    // ---------- автозапуск с Windows ----------

    static void SetAutostart(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (on) key.SetValue(AppName, $"\"{Environment.ProcessPath}\" --minimized");
            else key.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch { }
    }

    // ---------- окно и трей ----------

    void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    void ExitApp()
    {
        _exiting = true;
        _tray.Visible = false;
        Application.Exit();
    }

    protected override void SetVisibleCore(bool value)
    {
        // при старте с --minimized окно не показываем, сразу в трей
        if (_startHidden && !_shownOnce)
        {
            _shownOnce = true;
            if (!IsHandleCreated) CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosed(e);
    }
}
