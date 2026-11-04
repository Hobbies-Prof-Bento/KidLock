using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using KidLock.App.Native;

namespace KidLock.App.UI;

/// <summary>Desenha o cadeado em código: nítido em qualquer DPI e sem arquivos de imagem para manter.</summary>
internal static class IconFactory
{
    public static readonly Color LockedColor = Color.FromArgb(0xD1, 0x34, 0x38);
    public static readonly Color UnlockedColor = Color.FromArgb(0x10, 0x7C, 0x10);

    public static Icon CreateIcon(bool locked, int size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 8);
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            DrawPadlock(graphics, new RectangleF(0, 0, size, size), locked ? LockedColor : UnlockedColor, closed: locked);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    /// <summary>Desenha o cadeado numa grade lógica de 32×32 escalada para <paramref name="bounds"/>.</summary>
    public static void DrawPadlock(Graphics graphics, RectangleF bounds, Color color, bool closed)
    {
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TranslateTransform(bounds.X, bounds.Y);
            graphics.ScaleTransform(bounds.Width / 32f, bounds.Height / 32f);

            // Haste. Aberto: levantada, com a perna esquerda fora do corpo.
            var arcTop = closed ? 4f : 2f;
            var arcCenterY = arcTop + 6f;
            using (var pen = new Pen(color, 3.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var shackle = new GraphicsPath())
            {
                shackle.AddLine(10f, closed ? 15f : arcCenterY + 1f, 10f, arcCenterY);
                shackle.AddArc(10f, arcTop, 12f, 12f, 180f, 180f);
                shackle.AddLine(22f, arcCenterY, 22f, 15f);
                graphics.DrawPath(pen, shackle);
            }

            using (var body = RoundedRectangle(new RectangleF(5f, 14f, 22f, 16f), 3.5f))
            using (var brush = new SolidBrush(color))
            {
                graphics.FillPath(brush, body);
            }

            graphics.FillEllipse(Brushes.White, 14f, 18.5f, 4f, 4f);
            graphics.FillRectangle(Brushes.White, 15.1f, 21f, 1.8f, 4.5f);
        }
        finally
        {
            graphics.Restore(state);
        }
    }

    public static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
