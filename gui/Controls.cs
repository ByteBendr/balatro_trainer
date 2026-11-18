using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace BalatroTrainer.Gui;

internal class RoundedPanel : Panel
{
    private Color _fill = Theme.Card;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 14;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Edge { get; set; } = Theme.CardEdge;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Fill
    {
        get => _fill;
        set
        {
            _fill = value;
            BackColor = value;
            Invalidate();
        }
    }

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = _fill;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Felt);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
        using var fill = new SolidBrush(_fill);
        using var pen = new Pen(Edge, 1.5f);
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }
}

internal sealed class RoundedButton : Control
{
    private bool _hover;
    private bool _down;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Ghost { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 10;

    public RoundedButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = Theme.Button;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { _down = true; Invalidate(); }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Felt);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color face, shadow, text;
        if (!Enabled)
        {
            face = Color.FromArgb(0x2c, 0x46, 0x41);
            shadow = Color.FromArgb(0x26, 0x3d, 0x39);
            text = Color.FromArgb(0x7d, 0x95, 0x8d);
        }
        else if (Ghost)
        {
            face = _down ? Color.FromArgb(0x3a, 0x5a, 0x53) : _hover ? Color.FromArgb(0x4d, 0x76, 0x6b) : Color.FromArgb(0x41, 0x66, 0x5d);
            shadow = Color.FromArgb(0x1f, 0x35, 0x31);
            text = Theme.Cream;
        }
        else
        {
            face = _down ? Theme.AmberDown : _hover ? Theme.AmberHover : Theme.Amber;
            shadow = Theme.AmberShadow;
            text = Theme.Ink;
        }

        int lift = _down ? 2 : 0;
        var shadowRect = new Rectangle(0, 2, Width - 1, Height - 3);
        var faceRect = new Rectangle(0, lift, Width - 1, Height - 3);

        using (var path = Theme.Round(shadowRect, Radius))
        using (var b = new SolidBrush(shadow))
            g.FillPath(b, path);
        using (var path = Theme.Round(faceRect, Radius))
        using (var b = new SolidBrush(face))
            g.FillPath(b, path);

        TextRenderer.DrawText(g, Text, Font, faceRect, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class StatusPill : Control
{
    private string _text = "";
    private Color _dot = Theme.Muted;

    public StatusPill()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Font = Theme.BodyBold;
    }

    public void SetState(string text, Color dot)
    {
        if (text == _text && dot == _dot) return;
        _text = text;
        _dot = dot;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Felt);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2))
        using (var fill = new SolidBrush(Theme.FeltDark))
        using (var pen = new Pen(Theme.CardEdge, 1.5f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        const int d = 12;
        using (var glow = new SolidBrush(Color.FromArgb(60, _dot)))
            g.FillEllipse(glow, 13, (Height - d) / 2 - 3, d + 6, d + 6);
        using (var dot = new SolidBrush(_dot))
            g.FillEllipse(dot, 16, (Height - d) / 2, d, d);

        TextRenderer.DrawText(g, _text, Font, new Rectangle(38, 0, Width - 50, Height), Theme.Cream,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
            TextFormatFlags.EndEllipsis);
    }
}

internal sealed class AmountBox : RoundedPanel
{
    private const long Limit = 1_000_000_000L;
    private readonly TextBox _text = new();

    public AmountBox(long initial, long step)
    {
        Fill = Theme.FeltDark;
        Radius = 10;
        Size = new Size(176, 38);

        var minus = new RoundedButton { Text = "−", Ghost = true, Radius = 8 };
        minus.SetBounds(3, 3, 32, 32);
        minus.Click += (_, _) => Value -= step;

        var plus = new RoundedButton { Text = "+", Ghost = true, Radius = 8 };
        plus.SetBounds(141, 3, 32, 32);
        plus.Click += (_, _) => Value += step;

        _text.BorderStyle = BorderStyle.None;
        _text.BackColor = Theme.FeltDark;
        _text.ForeColor = Theme.Cream;
        _text.Font = Theme.Input;
        _text.TextAlign = HorizontalAlignment.Center;
        _text.MaxLength = 12;
        _text.Width = 98;
        _text.Location = new Point(39, (Height - _text.Height) / 2);
        _text.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '-')
                e.Handled = true;
        };
        _text.Leave += (_, _) => Value = Value;

        Controls.Add(minus);
        Controls.Add(_text);
        Controls.Add(plus);
        Value = initial;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long Value
    {
        get => long.TryParse(_text.Text.Trim(), out long v) ? Math.Clamp(v, -Limit, Limit) : 0;
        set => _text.Text = Math.Clamp(value, -Limit, Limit).ToString();
    }
}
