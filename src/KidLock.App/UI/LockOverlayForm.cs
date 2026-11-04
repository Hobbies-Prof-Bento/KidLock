using KidLock.App.Native;

namespace KidLock.App.UI;

/// <summary>
/// Aviso "bloqueado" no topo da tela. Não rouba foco, não aparece na barra de tarefas e deixa
/// os cliques passarem através dele. Aparece por alguns segundos ao bloquear e sempre que alguém
/// tenta usar o teclado/touchpad, para não cobrir o vídeo que a criança está assistindo.
/// </summary>
internal sealed class LockOverlayForm : Form
{
    private const string Title = "Teclado e touchpad bloqueados";

    private readonly System.Windows.Forms.Timer _hideTimer = new();
    private readonly Font _titleFont = new("Segoe UI Semibold", 12f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _hintFont = new("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    private string? _hint;

    public LockOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        Opacity = 0.9;
        DoubleBuffered = true;
        Font = _titleFont;
        _hideTimer.Tick += (_, _) => HideOverlay();
    }

    /// <summary>Segunda linha opcional (ex.: o atalho de desbloqueio).</summary>
    public string? Hint
    {
        get => _hint;
        set
        {
            _hint = value;
            if (Visible)
            {
                LayoutOverlay();
                Invalidate();
            }
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE
                | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOPMOST;
            return cp;
        }
    }

    private float DpiScale => DeviceDpi / 96f;

    public void Flash(TimeSpan duration)
    {
        LayoutOverlay();
        if (!Visible)
        {
            Show();
        }

        _hideTimer.Stop();
        _hideTimer.Interval = (int)duration.TotalMilliseconds;
        _hideTimer.Start();
        Invalidate();
    }

    public void HideOverlay()
    {
        _hideTimer.Stop();
        if (Visible)
        {
            Hide();
        }
    }

    /// <summary>Calcula tamanho e posição (centralizado no topo da tela principal).</summary>
    internal void LayoutOverlay()
    {
        var (padding, iconSize, gap) = Metrics();
        var titleSize = TextRenderer.MeasureText(Title, Font);
        var hintSize = _hint is null ? Size.Empty : TextRenderer.MeasureText(_hint, _hintFont);
        var textHeight = titleSize.Height + hintSize.Height;

        Size = new Size(
            (padding * 2) + iconSize + gap + Math.Max(titleSize.Width, hintSize.Width),
            (padding * 2) + Math.Max(iconSize, textHeight));

        var area = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        Location = new Point(area.Left + ((area.Width - Width) / 2), area.Top + (int)(24 * DpiScale));

        using var shape = IconFactory.RoundedRectangle(new RectangleF(0, 0, Width, Height), 12 * DpiScale);
        var oldRegion = Region;
        Region = new Region(shape);
        oldRegion?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var (padding, iconSize, gap) = Metrics();
        IconFactory.DrawPadlock(
            e.Graphics,
            new RectangleF(padding, (Height - iconSize) / 2f, iconSize, iconSize),
            IconFactory.LockedColor,
            closed: true);

        var titleSize = TextRenderer.MeasureText(Title, Font);
        var hintHeight = _hint is null ? 0 : TextRenderer.MeasureText(_hint, _hintFont).Height;
        var textX = padding + iconSize + gap;
        var textY = (Height - titleSize.Height - hintHeight) / 2;
        TextRenderer.DrawText(e.Graphics, Title, Font, new Point(textX, textY), ForeColor);
        if (_hint is not null)
        {
            TextRenderer.DrawText(e.Graphics, _hint, _hintFont, new Point(textX, textY + titleSize.Height), Color.FromArgb(200, 200, 200));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
            Region?.Dispose();
        }

        base.Dispose(disposing);
        if (disposing)
        {
            _titleFont.Dispose();
            _hintFont.Dispose();
        }
    }

    private (int Padding, int IconSize, int Gap) Metrics()
    {
        var scale = DpiScale;
        return ((int)(16 * scale), (int)(28 * scale), (int)(12 * scale));
    }
}
