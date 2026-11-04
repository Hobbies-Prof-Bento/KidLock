using KidLock.App.UI;

namespace KidLock.App.Tests.UI;

/// <summary>
/// Renderiza ícones e o aviso em bitmaps. Defina KIDLOCK_RENDER_DIR para salvar os PNGs e conferir
/// visualmente.
/// </summary>
public class RenderingTests
{
    [Theory]
    [InlineData(true, 16)]
    [InlineData(true, 32)]
    [InlineData(true, 48)]
    [InlineData(false, 24)]
    [InlineData(false, 48)]
    public void TrayIcon_HasRequestedSize_AndStateColor(bool locked, int size)
    {
        using var icon = IconFactory.CreateIcon(locked, size);
        using var bitmap = icon.ToBitmap();
        Save(bitmap, $"icon-{(locked ? "locked" : "unlocked")}-{size}.png");

        Assert.Equal(new Size(size, size), icon.Size);
        var expected = locked ? IconFactory.LockedColor : IconFactory.UnlockedColor;
        var bodyPixel = bitmap.GetPixel(size * 8 / 32, size * 26 / 32); // canto inferior esquerdo do corpo
        Assert.True(IsClose(expected, bodyPixel), $"cor esperada {expected}, obtida {bodyPixel}");
        Assert.Equal(0, bitmap.GetPixel(0, 0).A); // fundo transparente
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Para desbloquear: Ctrl+Alt+Shift+L")]
    public void Overlay_RendersWithoutErrors(string? hint)
    {
        RunOnStaThread(() =>
        {
            using var overlay = new LockOverlayForm { Hint = hint };
            overlay.LayoutOverlay();

            Assert.True(overlay.Width > overlay.Height * 3, "o aviso deve ser uma faixa horizontal");
            using var bitmap = new Bitmap(overlay.Width, overlay.Height);
            overlay.DrawToBitmap(bitmap, new Rectangle(Point.Empty, overlay.Size));
            Save(bitmap, hint is null ? "overlay.png" : "overlay-hint.png");
        });
    }

    private static bool IsClose(Color a, Color b) =>
        Math.Abs(a.R - b.R) < 40 && Math.Abs(a.G - b.G) < 40 && Math.Abs(a.B - b.B) < 40 && b.A > 200;

    /// <summary>Desenha o controle num bitmap e salva se KIDLOCK_RENDER_DIR estiver definida.</summary>
    internal static void SaveForInspection(Control control, string name)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        Save(bitmap, name);
    }

    private static void Save(Bitmap bitmap, string name)
    {
        if (Environment.GetEnvironmentVariable("KIDLOCK_RENDER_DIR") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            bitmap.Save(Path.Combine(directory, name), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private static void RunOnStaThread(Action action) => StaThread.Run(action);
}
