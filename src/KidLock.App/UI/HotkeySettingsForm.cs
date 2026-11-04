using KidLock.Core.Hotkeys;
using KidLock.Core.Locking;
using KidLock.Core.Settings;

namespace KidLock.App.UI;

/// <summary>
/// Mostra os atalhos em uso e permite trocá-los. Abre com duplo clique no cadeado e na primeira
/// execução, para ninguém bloquear o teclado sem saber como desbloquear.
/// </summary>
/// <remarks>
/// A nova combinação é capturada pelo gancho de teclado (<see cref="InputFilter.BeginHotkeyCapture"/>),
/// não pela janela: assim funcionam até combinações com Win, e o atalho atual não bloqueia nada
/// enquanto a pessoa escolhe outro.
/// </remarks>
internal sealed class HotkeySettingsForm : Form
{
    private const string CapturePrompt = "Aperte a nova combinação… (Esc cancela)";
    private static readonly Color CaptureBackColor = Color.FromArgb(255, 244, 206);

    private readonly InputFilter _filter;
    private readonly SynchronizationContext _ui;
    private readonly AppSettings _settings;
    private readonly Action<AppSettings> _save;
    private readonly Action _lockNow;
    private readonly HotkeySettingsModel _model;
    private readonly Font _baseFont = new("Segoe UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _valueFont = new("Segoe UI Semibold", 15f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _headingFont = new("Segoe UI Semibold", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Label _lockCaption;
    private readonly Label _lockValue;
    private readonly Button _changeLock;
    private readonly CheckBox _separateUnlock;
    private readonly Label _unlockCaption;
    private readonly Label _unlockValue;
    private readonly Button _changeUnlock;
    private readonly Label _error;
    private readonly Button _lockNowButton;
    private readonly Button _saveButton;
    private readonly Button _closeButton;
    private Label? _capturing;

    public HotkeySettingsForm(
        AppSettings settings,
        InputFilter filter,
        SynchronizationContext ui,
        Action<AppSettings> save,
        Action lockNow)
    {
        _settings = settings;
        _filter = filter;
        _ui = ui;
        _save = save;
        _lockNow = lockNow;
        _model = new HotkeySettingsModel(settings);

        Text = "KidLock: atalhos de teclado";
        Font = _baseFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(Px(20));
        Icon = LoadAppIcon();

        var intro = WrappedLabel("Use estas teclas para bloquear e desbloquear o teclado e o touchpad:");
        _error = WrappedLabel(string.Empty);
        _error.ForeColor = Color.FromArgb(0xC4, 0x2B, 0x1C);
        _lockCaption = new Label { AutoSize = true, Font = _headingFont, Margin = new Padding(0, Px(14), 0, Px(4)) };
        _lockValue = ValueLabel();
        _changeLock = ActionButton("Alterar…", (_, _) => BeginCapture(_lockValue));
        _separateUnlock = new CheckBox
        {
            AutoSize = true,
            Text = "Usar outro atalho para desbloquear",
            Margin = new Padding(0, Px(10), 0, 0),
            Checked = _model.SeparateUnlock,
        };
        _separateUnlock.CheckedChanged += (_, _) =>
        {
            _model.SeparateUnlock = _separateUnlock.Checked;
            _error.Text = _model.ValidationError ?? string.Empty;
            RefreshView();
        };
        _unlockCaption = new Label { AutoSize = true, Font = _headingFont, Text = "Desbloquear", Margin = _lockCaption.Margin };
        _unlockValue = ValueLabel();
        _changeUnlock = ActionButton("Alterar…", (_, _) => BeginCapture(_unlockValue));
        var emergency = WrappedLabel(
            "Esqueceu o atalho? Aperte Ctrl + Alt + Del, escolha \"Bloquear\" e entre de novo com a sua senha: " +
            "o KidLock libera o teclado e o touchpad.");
        emergency.ForeColor = SystemColors.GrayText;
        emergency.Margin = new Padding(0, Px(14), 0, 0);

        _lockNowButton = ActionButton("Bloquear agora", (_, _) =>
        {
            Close();
            _lockNow();
        });
        _saveButton = ActionButton("Salvar", (_, _) => SaveAndClose());
        _closeButton = ActionButton("Fechar", (_, _) => Close());
        CancelButton = _closeButton;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, Px(18), 0, 0),
            WrapContents = false,
        };
        buttons.Controls.AddRange([_closeButton, _saveButton, _lockNowButton]);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddSpanning(layout, intro);
        AddSpanning(layout, _lockCaption);
        AddRow(layout, _lockValue, _changeLock);
        AddSpanning(layout, _separateUnlock);
        AddSpanning(layout, _unlockCaption);
        AddRow(layout, _unlockValue, _changeUnlock);
        AddSpanning(layout, _error);
        AddSpanning(layout, emergency);
        AddSpanning(layout, buttons);
        Controls.Add(layout);

        RefreshView();
    }

    /// <summary>Texto mostrado para o atalho de bloqueio (para testes).</summary>
    internal string LockHotkeyText => _lockValue.Text;

    internal string ErrorText => _error.Text;

    internal bool CanSave => _saveButton.Enabled;

    /// <summary>Começa a capturar a próxima combinação para o atalho de bloqueio (true) ou de desbloqueio.</summary>
    internal void BeginCapture(bool forLock) => BeginCapture(forLock ? _lockValue : _unlockValue);

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_capturing is not null)
        {
            _capturing = null;
            _filter.CancelHotkeyCapture();
        }

        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _baseFont.Dispose();
            _valueFont.Dispose();
            _headingFont.Dispose();
        }
    }

    private void BeginCapture(Label target)
    {
        _capturing = target;
        _error.Text = string.Empty;
        target.Text = CapturePrompt;
        target.BackColor = CaptureBackColor;
        SetButtonsEnabled(false);
        _filter.BeginHotkeyCapture(hotkey => _ui.Post(_ => OnCaptured(target, hotkey), null));
    }

    private void OnCaptured(Label target, Hotkey? hotkey)
    {
        if (IsDisposed || _capturing != target)
        {
            return;
        }

        _capturing = null;
        target.BackColor = SystemColors.Window;
        if (hotkey is not null)
        {
            var problem = target == _lockValue ? _model.TrySetLockHotkey(hotkey) : _model.TrySetUnlockHotkey(hotkey);
            _error.Text = problem ?? _model.ValidationError ?? string.Empty;
        }

        SetButtonsEnabled(true);
        RefreshView();
    }

    private void SaveAndClose()
    {
        _save(_model.ApplyTo(_settings));
        Close();
    }

    private void RefreshView()
    {
        var separate = _model.SeparateUnlock;
        _lockCaption.Text = separate ? "Bloquear" : "Bloquear e desbloquear";
        if (_capturing != _lockValue)
        {
            _lockValue.Text = _model.LockHotkey.ToDisplayString();
        }

        if (_capturing != _unlockValue)
        {
            _unlockValue.Text = _model.UnlockHotkey.ToDisplayString();
        }

        _unlockCaption.Visible = separate;
        _unlockValue.Visible = separate;
        _changeUnlock.Visible = separate;
        _error.Visible = _error.Text.Length > 0;
        if (_capturing is null)
        {
            _saveButton.Enabled = _model.CanSave;
            _lockNowButton.Enabled = !_model.HasChanges;
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _changeLock.Enabled = enabled;
        _changeUnlock.Enabled = enabled;
        _separateUnlock.Enabled = enabled;
        _saveButton.Enabled = enabled && _model.CanSave;
        _lockNowButton.Enabled = enabled && !_model.HasChanges;
    }

    private Label ValueLabel() => new()
    {
        AutoSize = true,
        Font = _valueFont,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = SystemColors.Window,
        Padding = new Padding(Px(14), Px(8), Px(14), Px(8)),
        MinimumSize = new Size(Px(320), 0),
        TextAlign = ContentAlignment.MiddleCenter,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        Margin = new Padding(0, 0, Px(10), 0),
    };

    private Label WrappedLabel(string text) => new()
    {
        AutoSize = true,
        Text = text,
        MaximumSize = new Size(Px(460), 0),
        Margin = new Padding(0, Px(6), 0, 0),
    };

    private Button ActionButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            Padding = new Padding(Px(8), Px(2), Px(8), Px(2)),
            Anchor = AnchorStyles.Left,
        };
        button.Click += onClick;
        return button;
    }

    private static void AddSpanning(TableLayoutPanel layout, Control control)
    {
        layout.Controls.Add(control);
        layout.SetColumnSpan(control, 2);
    }

    private static void AddRow(TableLayoutPanel layout, Control left, Control right)
    {
        layout.Controls.Add(left);
        layout.Controls.Add(right);
    }

    private int Px(int pixels) => (int)Math.Round(pixels * DeviceDpi / 96f);

    private static Icon? LoadAppIcon()
    {
        try
        {
            return Environment.ProcessPath is { } path ? Icon.ExtractAssociatedIcon(path) : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
