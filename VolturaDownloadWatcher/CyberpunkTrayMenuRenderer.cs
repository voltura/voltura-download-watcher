namespace VolturaDownloadWatcher;

internal sealed class CyberpunkTrayMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer
{
    private static readonly System.Drawing.Color ElectricGreen = System.Drawing.Color.FromArgb(95, 210, 122);
    private static readonly System.Drawing.Color MutedGreen = System.Drawing.Color.FromArgb(49, 111, 67);
    private static readonly System.Drawing.Color SelectedGreen = System.Drawing.Color.FromArgb(28, 71, 42);

    internal CyberpunkTrayMenuRenderer()
        : base(new CyberpunkTrayColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBorder(System.Windows.Forms.ToolStripRenderEventArgs e)
    {
        using var pen = new System.Drawing.Pen(MutedGreen);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs e)
    {
        var backgroundColor = e.ToolStrip?.BackColor ?? System.Drawing.Color.FromArgb(7, 16, 11);
        using var brush = new System.Drawing.SolidBrush(e.Item.Selected ? SelectedGreen : backgroundColor);
        e.Graphics.FillRectangle(brush, new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size));
    }

    protected override void OnRenderSeparator(System.Windows.Forms.ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new System.Drawing.Pen(MutedGreen);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
    }

    protected override void OnRenderArrow(System.Windows.Forms.ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = ElectricGreen;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(System.Windows.Forms.ToolStripItemImageRenderEventArgs e)
    {
        const int size = 12;
        var x = e.ImageRectangle.Left + ((e.ImageRectangle.Width - size) / 2);
        var y = e.ImageRectangle.Top + ((e.ImageRectangle.Height - size) / 2);
        var bounds = new System.Drawing.Rectangle(x, y, size, size);
        using var background = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(8, 18, 12));
        using var border = new System.Drawing.Pen(ElectricGreen, 1.2f);
        using var check = new System.Drawing.Pen(ElectricGreen, 1.8f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        e.Graphics.FillRectangle(background, bounds);
        e.Graphics.DrawRectangle(border, bounds);
        e.Graphics.DrawLines(check,
        [
            new System.Drawing.Point(x + 2, y + 6),
            new System.Drawing.Point(x + 5, y + 9),
            new System.Drawing.Point(x + 10, y + 3)
        ]);
    }

    private sealed class CyberpunkTrayColorTable : System.Windows.Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(7, 16, 11);
        public override System.Drawing.Color ImageMarginGradientBegin => System.Drawing.Color.FromArgb(7, 16, 11);
        public override System.Drawing.Color ImageMarginGradientMiddle => System.Drawing.Color.FromArgb(7, 16, 11);
        public override System.Drawing.Color ImageMarginGradientEnd => System.Drawing.Color.FromArgb(7, 16, 11);
        public override System.Drawing.Color MenuBorder => MutedGreen;
        public override System.Drawing.Color MenuItemBorder => MutedGreen;
        public override System.Drawing.Color MenuItemSelected => SelectedGreen;
        public override System.Drawing.Color SeparatorDark => MutedGreen;
        public override System.Drawing.Color SeparatorLight => MutedGreen;
    }
}
