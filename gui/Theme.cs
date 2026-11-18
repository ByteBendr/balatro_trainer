using System.Drawing.Drawing2D;

namespace BalatroTrainer.Gui;

internal static class Theme
{
    public static readonly Color Felt = Color.FromArgb(0x24, 0x3b, 0x37);
    public static readonly Color FeltDark = Color.FromArgb(0x1a, 0x2c, 0x29);
    public static readonly Color Card = Color.FromArgb(0x31, 0x4f, 0x49);
    public static readonly Color CardEdge = Color.FromArgb(0x41, 0x66, 0x5d);

    public static readonly Color Cream = Color.FromArgb(0xf4, 0xea, 0xd3);
    public static readonly Color Muted = Color.FromArgb(0xa9, 0xbf, 0xb6);
    public static readonly Color Ink = Color.FromArgb(0x2a, 0x1f, 0x14);

    public static readonly Color Amber = Color.FromArgb(0xee, 0xa6, 0x3a);
    public static readonly Color AmberHover = Color.FromArgb(0xf6, 0xb9, 0x5a);
    public static readonly Color AmberDown = Color.FromArgb(0xd4, 0x90, 0x2a);
    public static readonly Color AmberShadow = Color.FromArgb(0x9a, 0x65, 0x17);

    public static readonly Color Green = Color.FromArgb(0x7f, 0xd1, 0x8b);
    public static readonly Color Red = Color.FromArgb(0xe8, 0x68, 0x5f);
    public static readonly Color Blue = Color.FromArgb(0x5f, 0xb0, 0xe8);

    public static readonly Font Title = new("Georgia", 22f, FontStyle.Bold);
    public static readonly Font Section = new("Georgia", 12f, FontStyle.Bold | FontStyle.Italic);
    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font BodyBold = new("Segoe UI Semibold", 10f);
    public static readonly Font Button = new("Segoe UI Semibold", 9.5f);
    public static readonly Font Input = new("Segoe UI Semibold", 11f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font Suit = new("Segoe UI Symbol", 16f);

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        int d = Math.Max(2, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
