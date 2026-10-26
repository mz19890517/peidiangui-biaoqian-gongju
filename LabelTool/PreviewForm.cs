using System.Drawing.Drawing2D;

namespace LabelTool;

/// <summary>预览中的一页</summary>
public class PreviewPageInfo
{
    public string Printer { get; set; } = "";
    public List<PlacedLabel> Labels { get; set; } = new();
    public float PaperW { get; set; }
    public float PaperH { get; set; }
    public bool Single { get; set; }
}

/// <summary>预览里的字体/间距覆盖设置</summary>
public class FontOverride
{
    public string? Family { get; set; }
    public double Size { get; set; }
    public bool AutoFit { get; set; }
    public bool AutoSize { get; set; }
    public double LineSpacing { get; set; }
    public double CharSpacing { get; set; }
    public double Weight { get; set; }
}

/// <summary>预览内容来源（一台打印机的全部标签，或一个批次）</summary>
public class PreviewSource
{
    public string Name { get; set; } = "";
    public List<PreviewPageInfo> Pages { get; set; } = new();
    public Action? PrintAction { get; set; }

    /// <summary>该来源包含的标签类型（用于预览里保存字体）</summary>
    public List<string> TypeKeys { get; set; } = new();
    public bool HasCurrentType { get; set; }
    public string? FontFamily { get; set; }
    public double FontSize { get; set; }
    public bool AutoFit { get; set; }
    public bool AutoSize { get; set; }
    public double LineSpacing { get; set; }
    public double CharSpacing { get; set; }
    public double Weight { get; set; }
}

/// <summary>独立预览窗口：内容下拉、页码、字体实时调整、滚轮缩放、拖动平移、点击定位、打印</summary>
public class PreviewForm : Form
{
    private readonly ComboBox cmbSource = new();
    private readonly ComboBox cmbPage = new();
    private readonly CheckBox chkFit = new();
    private readonly Label lblZoom = new();
    private readonly Panel scroll = new();
    private readonly PictureBox _pic = new();

    private readonly ComboBox cmbFontFamily = new();
    private readonly NumericUpDown numFont = new();
    private readonly CheckBox chkAutoFit = new();
    private readonly CheckBox chkAutoSize = new();
    private readonly NumericUpDown numLineSpacing = new();
    private readonly NumericUpDown numCharSpacing = new();
    private readonly NumericUpDown numWeight = new();
    private readonly Button btnSaveFont = new();

    private List<PreviewSource> _sources = new();
    private Action<int>? _onLabelClick;
    private Action? _onRefreshRequest;
    private Action<int, FontOverride>? _onFontChange;
    private Action? _onFontSave;
    private bool _updating;

    private const float Dpi = 120f;
    private Bitmap? _base;
    private readonly List<(Rectangle rect, int index)> _hits = new();
    private float _zoom = 1f;
    private bool _dragging;
    private bool _moved;
    private Point _dragStart;
    private Point _scrollStart;

    public PreviewForm()
    {
        Text = "预览";
        ClientSize = new Size(940, 920);
        Font = new Font("微软雅黑", 9F);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 122 };
        var bar = new Panel { Left = 0, Top = 0, Height = 40, Width = 940, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        var bar2 = new Panel { Left = 0, Top = 40, Height = 40, Width = 940, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        var bar3 = new Panel { Left = 0, Top = 80, Height = 40, Width = 940, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        // ---- 第一行：内容 / 页码 / 打印 / 缩放 ----
        bar.Controls.Add(new Label { Text = "内容:", Left = 8, Top = 12, AutoSize = true });
        cmbSource.Left = 50; cmbSource.Top = 8; cmbSource.Width = 250;
        cmbSource.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSource.SelectedIndexChanged += (_, _) => { cmbPage.SelectedIndex = -1; Render(); LoadFontControls(); };
        bar.Controls.Add(cmbSource);

        bar.Controls.Add(new Label { Text = "页码:", Left = 312, Top = 12, AutoSize = true });
        cmbPage.Left = 352; cmbPage.Top = 8; cmbPage.Width = 110;
        cmbPage.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPage.SelectedIndexChanged += (_, _) => Render();
        bar.Controls.Add(cmbPage);

        var btnRefresh = new Button { Text = "刷新", Left = 474, Top = 7, Width = 56, Height = 26 };
        btnRefresh.Click += (_, _) => _onRefreshRequest?.Invoke();
        bar.Controls.Add(btnRefresh);

        var btnPrint = new Button { Text = "打印当前", Left = 536, Top = 7, Width = 90, Height = 26 };
        btnPrint.Click += (_, _) =>
        {
            int si = cmbSource.SelectedIndex;
            if (si >= 0 && si < _sources.Count) _sources[si].PrintAction?.Invoke();
        };
        bar.Controls.Add(btnPrint);

        chkFit.Text = "适应宽度"; chkFit.Left = 640; chkFit.Top = 10; chkFit.AutoSize = true;
        chkFit.Checked = true;
        chkFit.CheckedChanged += (_, _) => { if (chkFit.Checked) FitToWidth(); };
        bar.Controls.Add(chkFit);

        var btnZoomIn = new Button { Text = "+", Left = 720, Top = 7, Width = 30, Height = 26 };
        var btnZoomOut = new Button { Text = "-", Left = 754, Top = 7, Width = 30, Height = 26 };
        bar.Controls.AddRange(new Control[] { btnZoomIn, btnZoomOut });
        btnZoomIn.Click += (_, _) => { chkFit.Checked = false; SetZoom(_zoom * 1.25f); };
        btnZoomOut.Click += (_, _) => { chkFit.Checked = false; SetZoom(_zoom / 1.25f); };

        lblZoom.Left = 792; lblZoom.Top = 13; lblZoom.AutoSize = true; lblZoom.ForeColor = Color.DimGray;
        bar.Controls.Add(lblZoom);

        // ---- 第二行：字体实时调整 ----
        bar2.Controls.Add(new Label { Text = "字体:", Left = 8, Top = 12, AutoSize = true });
        cmbFontFamily.Left = 46; cmbFontFamily.Top = 8; cmbFontFamily.Width = 150;
        cmbFontFamily.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbFontFamily.Items.AddRange(new object[] { "（用预设）", "宋体", "黑体", "微软雅黑", "楷体" });
        cmbFontFamily.SelectedIndex = 0;
        cmbFontFamily.SelectedIndexChanged += (_, _) => FireFontChange();
        bar2.Controls.Add(cmbFontFamily);

        bar2.Controls.Add(new Label { Text = "字号:", Left = 210, Top = 12, AutoSize = true });
        numFont.Left = 250; numFont.Top = 8; numFont.Width = 70;
        numFont.Minimum = 0; numFont.Maximum = 300; numFont.DecimalPlaces = 1; numFont.Increment = 0.5m; numFont.Value = 0;
        numFont.ValueChanged += (_, _) => FireFontChange();
        bar2.Controls.Add(numFont);

        chkAutoFit.Text = "自动缩小适应框"; chkAutoFit.Left = 334; chkAutoFit.Top = 10; chkAutoFit.AutoSize = true;
        chkAutoFit.CheckedChanged += (_, _) => FireFontChange();
        bar2.Controls.Add(chkAutoFit);

        btnSaveFont.Text = "保存字体到柜子"; btnSaveFont.Left = 500; btnSaveFont.Top = 7; btnSaveFont.Width = 130; btnSaveFont.Height = 26;
        btnSaveFont.Click += (_, _) => _onFontSave?.Invoke();
        bar2.Controls.Add(btnSaveFont);

        bar2.Controls.Add(new Label { Text = "（字号 0 = 用预设）", Left = 646, Top = 13, AutoSize = true, ForeColor = Color.DimGray });

        // ---- 第三行：行距 / 字距 / 粗细 / 自适应字号 ----
        bar3.Controls.Add(new Label { Text = "行距(mm):", Left = 8, Top = 12, AutoSize = true });
        numLineSpacing.Left = 74; numLineSpacing.Top = 8; numLineSpacing.Width = 70;
        numLineSpacing.Minimum = -10; numLineSpacing.Maximum = 20; numLineSpacing.DecimalPlaces = 2; numLineSpacing.Increment = 0.1m; numLineSpacing.Value = 0;
        numLineSpacing.ValueChanged += (_, _) => FireFontChange();
        bar3.Controls.Add(numLineSpacing);

        bar3.Controls.Add(new Label { Text = "字距(mm):", Left = 158, Top = 12, AutoSize = true });
        numCharSpacing.Left = 224; numCharSpacing.Top = 8; numCharSpacing.Width = 70;
        numCharSpacing.Minimum = -5; numCharSpacing.Maximum = 30; numCharSpacing.DecimalPlaces = 2; numCharSpacing.Increment = 0.1m; numCharSpacing.Value = 0;
        numCharSpacing.ValueChanged += (_, _) => FireFontChange();
        bar3.Controls.Add(numCharSpacing);

        bar3.Controls.Add(new Label { Text = "粗细(mm):", Left = 308, Top = 12, AutoSize = true });
        numWeight.Left = 374; numWeight.Top = 8; numWeight.Width = 70;
        numWeight.Minimum = 0; numWeight.Maximum = 1.5m; numWeight.DecimalPlaces = 2; numWeight.Increment = 0.05m; numWeight.Value = 0;
        numWeight.ValueChanged += (_, _) => FireFontChange();
        bar3.Controls.Add(numWeight);

        chkAutoSize.Text = "自适应字号"; chkAutoSize.Left = 460; chkAutoSize.Top = 10; chkAutoSize.AutoSize = true;
        chkAutoSize.CheckedChanged += (_, _) => FireFontChange();
        bar3.Controls.Add(chkAutoSize);

        bar3.Controls.Add(new Label { Text = "（行距/字距/粗细 0 = 用预设；自适应字号按框放大到最大）", Left = 570, Top = 13, AutoSize = true, ForeColor = Color.DimGray });

        topPanel.Controls.Add(bar);
        topPanel.Controls.Add(bar2);
        topPanel.Controls.Add(bar3);

        scroll.Dock = DockStyle.Fill;
        scroll.AutoScroll = true;
        scroll.BackColor = Color.FromArgb(225, 225, 225);
        _pic.SizeMode = PictureBoxSizeMode.StretchImage;
        _pic.BackColor = Color.FromArgb(225, 225, 225);
        _pic.MouseDown += Pic_MouseDown;
        _pic.MouseMove += Pic_MouseMove;
        _pic.MouseUp += Pic_MouseUp;
        MouseWheel += Form_MouseWheel;
        scroll.Resize += (_, _) => { if (chkFit.Checked) FitToWidth(); };
        scroll.Controls.Add(_pic);

        Controls.Add(scroll);
        Controls.Add(topPanel);
    }

    public void Initialize(Action<int> onLabelClick, Action onRefreshRequest,
        Action<int, FontOverride> onFontChange, Action onFontSave)
    {
        _onLabelClick = onLabelClick;
        _onRefreshRequest = onRefreshRequest;
        _onFontChange = onFontChange;
        _onFontSave = onFontSave;
    }

    public int CurrentSourceIndex => cmbSource.SelectedIndex;

    public void SetSources(List<PreviewSource> sources)
    {
        _sources = sources;
        string? prev = cmbSource.SelectedItem as string;
        _updating = true;
        cmbSource.Items.Clear();
        foreach (var s in sources) cmbSource.Items.Add(s.Name);
        _updating = false;

        if (cmbSource.Items.Count == 0)
        {
            cmbPage.Items.Clear();
            ClearImage();
            LoadFontControls();
            return;
        }
        int idx = prev != null ? cmbSource.Items.IndexOf(prev) : -1;
        cmbSource.SelectedIndex = idx >= 0 ? idx : 0;
        LoadFontControls();
    }

    private void LoadFontControls()
    {
        int si = cmbSource.SelectedIndex;
        bool has = si >= 0 && si < _sources.Count;
        cmbFontFamily.Enabled = numFont.Enabled = chkAutoFit.Enabled = btnSaveFont.Enabled = has;
        numLineSpacing.Enabled = numCharSpacing.Enabled = numWeight.Enabled = chkAutoSize.Enabled = has;
        if (!has) return;

        var s = _sources[si];
        _updating = true;
        try
        {
            string? fam = s.FontFamily;
            if (string.IsNullOrEmpty(fam)) cmbFontFamily.SelectedIndex = 0;
            else
            {
                int i = cmbFontFamily.Items.IndexOf(fam);
                if (i < 0) { cmbFontFamily.Items.Add(fam); i = cmbFontFamily.Items.Count - 1; }
                cmbFontFamily.SelectedIndex = i;
            }
            numFont.Value = (decimal)Math.Min(Math.Max(s.FontSize, 0), 300);
            chkAutoFit.Checked = s.AutoFit;
            chkAutoSize.Checked = s.AutoSize;
            numLineSpacing.Value = (decimal)Math.Min(Math.Max(s.LineSpacing, -10), 20);
            numCharSpacing.Value = (decimal)Math.Min(Math.Max(s.CharSpacing, -5), 30);
            numWeight.Value = (decimal)Math.Min(Math.Max(s.Weight, 0), 1.5);
        }
        finally { _updating = false; }
    }

    private void FireFontChange()
    {
        if (_updating) return;
        int si = cmbSource.SelectedIndex;
        if (si < 0 || si >= _sources.Count) return;
        var o = new FontOverride
        {
            Family = cmbFontFamily.SelectedIndex <= 0 ? null : cmbFontFamily.SelectedItem as string,
            Size = (double)numFont.Value,
            AutoFit = chkAutoFit.Checked,
            AutoSize = chkAutoSize.Checked,
            LineSpacing = (double)numLineSpacing.Value,
            CharSpacing = (double)numCharSpacing.Value,
            Weight = (double)numWeight.Value
        };
        _onFontChange?.Invoke(si, o);
    }

    private void ClearImage()
    {
        _pic.Image = null;
        if (_base != null) { _base.Dispose(); _base = null; }
        lblZoom.Text = "";
        _hits.Clear();
    }

    private void Render()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            int si = cmbSource.SelectedIndex;
            if (si < 0 || si >= _sources.Count) { cmbPage.Items.Clear(); ClearImage(); return; }
            var src = _sources[si];

            cmbPage.Items.Clear();
            for (int i = 0; i < src.Pages.Count; i++) cmbPage.Items.Add($"第 {i + 1} 页 / 共 {src.Pages.Count} 页");
            if (src.Pages.Count == 0) { cmbPage.SelectedIndex = -1; ClearImage(); return; }

            int pageIdx = Math.Min(Math.Max(cmbPage.SelectedIndex, 0), src.Pages.Count - 1);
            if (cmbPage.SelectedIndex != pageIdx) cmbPage.SelectedIndex = pageIdx;

            var page = src.Pages[pageIdx];
            float pw = page.Single && page.Labels.Count > 0 ? page.Labels[0].W : page.PaperW;
            float ph = page.Single && page.Labels.Count > 0 ? page.Labels[0].H : page.PaperH;

            var oldBase = _base;
            _base = null;
            _hits.Clear();

            using var bmp = PrintService.RenderPage(page.Labels, pw, ph, Dpi, Dpi);
            float pxmm = Dpi / 25.4f;
            foreach (var lb in page.Labels)
            {
                var rect = Rectangle.Round(new RectangleF(lb.X * pxmm, lb.Y * pxmm, lb.W * pxmm, lb.H * pxmm));
                _hits.Add((rect, lb.Index));
            }
            _base = (Bitmap)bmp.Clone();
            _pic.Image = _base;

            oldBase?.Dispose();
            if (chkFit.Checked) FitToWidth(); else SetZoom(_zoom);
        }
        catch { try { ClearImage(); } catch { } }
        finally { _updating = false; }
    }

    private void FitToWidth()
    {
        if (_base == null) return;
        float sx = (scroll.ClientSize.Width - 24f) / _base.Width;
        SetZoom(sx);
    }

    private void SetZoom(float zoom)
    {
        if (_base == null) return;
        int bw = _base.Width, bh = _base.Height;
        if (bw <= 0 || bh <= 0) return;

        _zoom = Math.Min(Math.Max(zoom, 0.03f), 8f);
        int w = Math.Max(1, (int)(bw * (double)_zoom));
        int h = Math.Max(1, (int)(bh * (double)_zoom));

        const int maxDim = 16000;
        if (w > maxDim || h > maxDim)
        {
            double s = Math.Min((double)maxDim / w, (double)maxDim / h);
            w = Math.Max(1, (int)(w * s));
            h = Math.Max(1, (int)(h * s));
            _zoom = (float)(_zoom * s);
        }

        _pic.Size = new Size(w, h);
        lblZoom.Text = $"缩放 {_zoom * 100:0}%";
    }

    private void Form_MouseWheel(object? sender, MouseEventArgs e)
    {
        if (_base == null) return;
        chkFit.Checked = false;

        Point screenPt = Cursor.Position;
        Point inScroll = scroll.PointToClient(screenPt);
        if (!scroll.ClientRectangle.Contains(inScroll)) return;

        Point curInPic = new(inScroll.X - _pic.Left, inScroll.Y - _pic.Top);
        float oldZoom = _zoom;
        float factor = e.Delta > 0 ? 1.15f : 1f / 1.15f;
        float newZoom = Math.Min(Math.Max(oldZoom * factor, 0.1f), 8f);
        if (Math.Abs(newZoom - oldZoom) < 0.0001f) return;

        float imgX = curInPic.X / oldZoom;
        float imgY = curInPic.Y / oldZoom;

        SetZoom(newZoom);

        int newLocX = (int)(inScroll.X - imgX * newZoom);
        int newLocY = (int)(inScroll.Y - imgY * newZoom);
        scroll.AutoScrollPosition = new Point(-newLocX, -newLocY);
    }

    private void Pic_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
        {
            _dragging = true;
            _moved = false;
            _dragStart = _pic.PointToScreen(e.Location);
            _scrollStart = scroll.AutoScrollPosition;
            _pic.Cursor = Cursors.Hand;
        }
    }

    private void Pic_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var p = _pic.PointToScreen(e.Location);
        int dx = p.X - _dragStart.X;
        int dy = p.Y - _dragStart.Y;
        if (Math.Abs(dx) + Math.Abs(dy) > 3) _moved = true;
        scroll.AutoScrollPosition = new Point(-(_scrollStart.X - dx), -(_scrollStart.Y - dy));
    }

    private void Pic_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _pic.Cursor = Cursors.Default;
        if (e.Button == MouseButtons.Left && !_moved)
        {
            float bx = e.X / _zoom, by = e.Y / _zoom;
            foreach (var (rect, index) in _hits)
            {
                if (rect.Contains((int)bx, (int)by) && index >= 0)
                {
                    _onLabelClick?.Invoke(index);
                    return;
                }
            }
        }
    }
}
