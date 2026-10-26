using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;

namespace LabelTool;

public partial class Form1 : Form
{
    private readonly PrintDocument _doc = new();
    private readonly List<PaperSize> _papers = new();

    // 打印时使用的当前位图与参数
    private Bitmap? _currentBitmap;
    private float _curWmm, _curHmm, _curOffX, _curOffY, _curAngle;

    private readonly ComboBox cmbPrinter = new();
    private readonly Button btnRefresh = new();
    private readonly ListBox lstPapers = new();
    private readonly Label lblInfo = new();

    private readonly NumericUpDown numW = new();
    private readonly NumericUpDown numH = new();
    private readonly NumericUpDown numFont = new();
    private readonly NumericUpDown numOffX = new();
    private readonly NumericUpDown numOffY = new();
    private readonly TextBox txtText = new();
    private readonly ComboBox cmbFont = new();
    private readonly ComboBox cmbRotate = new();
    private readonly PictureBox picPreview = new();
    private readonly Button btnSettings = new();
    private readonly Button btnPrint = new();

    public Form1()
    {
        InitializeComponent();
        BuildUi();
        LoadPrinters();
        UpdatePreview();
    }

    // ---------- 界面 ----------
    private void BuildUi()
    {
        Text = "配电柜标签工具 - 打印直连验证版";
        ClientSize = new Size(980, 640);
        Font = new Font("微软雅黑", 9F);
        StartPosition = FormStartPosition.CenterScreen;

        // 打印机
        var grpPrinter = new GroupBox { Text = "打印机", Left = 12, Top = 12, Width = 400, Height = 120 };
        grpPrinter.Controls.Add(new Label { Text = "打印机:", Left = 12, Top = 27, AutoSize = true });
        cmbPrinter.Left = 80; cmbPrinter.Top = 23; cmbPrinter.Width = 210;
        cmbPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPrinter.SelectedIndexChanged += (_, _) => OnPrinterChanged();
        grpPrinter.Controls.Add(cmbPrinter);
        btnRefresh.Text = "刷新"; btnRefresh.Left = 300; btnRefresh.Top = 22; btnRefresh.Width = 80; btnRefresh.Height = 25;
        btnRefresh.Click += (_, _) => LoadPrinters();
        grpPrinter.Controls.Add(btnRefresh);
        lblInfo.Left = 12; lblInfo.Top = 55; lblInfo.Width = 378; lblInfo.Height = 58;
        lblInfo.ForeColor = Color.DimGray;
        grpPrinter.Controls.Add(lblInfo);

        // 标签参数
        var grpParam = new GroupBox { Text = "标签参数", Left = 12, Top = 140, Width = 400, Height = 230 };
        grpParam.Controls.Add(new Label { Text = "宽(mm):", Left = 12, Top = 32, AutoSize = true });
        SetupNum(numW, 80, 28, 1, 2000, 40, 1, 0.5m); grpParam.Controls.Add(numW);
        grpParam.Controls.Add(new Label { Text = "高(mm):", Left = 205, Top = 32, AutoSize = true });
        SetupNum(numH, 273, 28, 1, 2000, 30, 1, 0.5m); grpParam.Controls.Add(numH);

        grpParam.Controls.Add(new Label { Text = "字号(pt):", Left = 12, Top = 64, AutoSize = true });
        SetupNum(numFont, 80, 60, 1, 300, 11, 1, 1); grpParam.Controls.Add(numFont);
        grpParam.Controls.Add(new Label { Text = "旋转:", Left = 205, Top = 64, AutoSize = true });
        cmbRotate.Left = 273; cmbRotate.Top = 60; cmbRotate.Width = 90;
        cmbRotate.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbRotate.Items.AddRange(new object[] { "0", "90", "180", "270" });
        cmbRotate.SelectedIndex = 0;
        cmbRotate.SelectedIndexChanged += (_, _) => UpdatePreview();
        grpParam.Controls.Add(cmbRotate);

        grpParam.Controls.Add(new Label { Text = "字体:", Left = 12, Top = 96, AutoSize = true });
        cmbFont.Left = 80; cmbFont.Top = 92; cmbFont.Width = 150;
        cmbFont.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbFont.Items.AddRange(new object[] { "宋体", "黑体", "微软雅黑", "楷体" });
        cmbFont.SelectedIndex = 0;
        cmbFont.SelectedIndexChanged += (_, _) => UpdatePreview();
        grpParam.Controls.Add(cmbFont);

        grpParam.Controls.Add(new Label { Text = "文字:", Left = 12, Top = 128, AutoSize = true });
        txtText.Left = 80; txtText.Top = 124; txtText.Width = 283;
        txtText.Text = "交流开关";
        txtText.TextChanged += (_, _) => UpdatePreview();
        grpParam.Controls.Add(txtText);

        grpParam.Controls.Add(new Label { Text = "偏移X(mm):", Left = 12, Top = 160, AutoSize = true });
        SetupNum(numOffX, 80, 156, -50, 50, 0, 1, 0.5m); grpParam.Controls.Add(numOffX);
        grpParam.Controls.Add(new Label { Text = "偏移Y(mm):", Left = 205, Top = 160, AutoSize = true });
        SetupNum(numOffY, 273, 156, -50, 50, 0, 1, 0.5m); grpParam.Controls.Add(numOffY);

        // 驱动纸张
        var grpPaper = new GroupBox { Text = "驱动支持的纸张（打印时选用）", Left = 12, Top = 378, Width = 400, Height = 185 };
        lstPapers.Left = 12; lstPapers.Top = 22; lstPapers.Width = 376; lstPapers.Height = 150;
        lstPapers.IntegralHeight = false;
        grpPaper.Controls.Add(lstPapers);

        // 底部按钮
        btnSettings.Text = "打印设置..."; btnSettings.Left = 12; btnSettings.Top = 575; btnSettings.Width = 150; btnSettings.Height = 34;
        btnSettings.Click += (_, _) => ShowPrintSettings();
        btnPrint.Text = "打印测试标签"; btnPrint.Left = 172; btnPrint.Top = 575; btnPrint.Width = 240; btnPrint.Height = 34;
        btnPrint.Click += (_, _) => DoPrint();

        // 预览
        picPreview.Left = 430; picPreview.Top = 22; picPreview.Width = 530; picPreview.Height = 590;
        picPreview.BorderStyle = BorderStyle.FixedSingle;
        picPreview.BackColor = Color.White;
        picPreview.SizeMode = PictureBoxSizeMode.Zoom;

        Controls.AddRange(new Control[]
        {
            grpPrinter, grpParam, grpPaper, btnSettings, btnPrint, picPreview
        });
    }

    private static void SetupNum(NumericUpDown n, int left, int top, decimal min, decimal max, decimal val, int decimals, decimal step)
    {
        n.Left = left; n.Top = top; n.Width = 90;
        n.Minimum = min; n.Maximum = max; n.Value = val; n.DecimalPlaces = decimals; n.Increment = step;
        n.ValueChanged += (_, _) => { };
    }

    // ---------- 打印机 ----------
    private void LoadPrinters()
    {
        string? prev = cmbPrinter.SelectedItem as string;
        cmbPrinter.Items.Clear();
        foreach (string p in PrinterSettings.InstalledPrinters)
            cmbPrinter.Items.Add(p);
        if (cmbPrinter.Items.Count == 0)
        {
            lblInfo.Text = "未检测到任何打印机";
            return;
        }
        if (prev != null && cmbPrinter.Items.Contains(prev))
            cmbPrinter.SelectedItem = prev;
        else
        {
            string def = new PrinterSettings().PrinterName;
            int idx = cmbPrinter.Items.IndexOf(def);
            cmbPrinter.SelectedIndex = idx >= 0 ? idx : 0;
        }
    }

    private void OnPrinterChanged()
    {
        if (cmbPrinter.SelectedItem is not string name) return;
        try
        {
            _doc.PrinterSettings.PrinterName = name;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "无法使用该打印机：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        RefreshPaperList();
        RefreshPrinterInfo();
        UpdatePreview();
    }

    private void RefreshPaperList()
    {
        _papers.Clear();
        lstPapers.Items.Clear();
        try
        {
            foreach (PaperSize ps in _doc.PrinterSettings.PaperSizes)
            {
                _papers.Add(ps);
                double w = ps.Width / 100.0 * 25.4;
                double h = ps.Height / 100.0 * 25.4;
                lstPapers.Items.Add($"{ps.PaperName}   {w:0.#}×{h:0.#} mm");
            }
            // 尝试自动选中与标签尺寸最接近的纸
            if (_papers.Count > 0) lstPapers.SelectedIndex = 0;
        }
        catch { }
    }

    private void RefreshPrinterInfo()
    {
        var ps = _doc.PrinterSettings;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"打印机: {ps.PrinterName}");
        try
        {
            using var mg = ps.CreateMeasurementGraphics();
            sb.AppendLine($"驱动DPI: {mg.DpiX:0} × {mg.DpiY:0}");
        }
        catch { }
        try
        {
            var pa = ps.DefaultPageSettings.PrintableArea;
            sb.AppendLine($"可打印区: {pa.Width / 100.0 * 25.4:0.#} × {pa.Height / 100.0 * 25.4:0.#} mm");
            sb.AppendLine($"硬边距: 左{ps.DefaultPageSettings.HardMarginX / 100.0 * 25.4:0.#} 上{ps.DefaultPageSettings.HardMarginY / 100.0 * 25.4:0.#} mm");
        }
        catch { }
        lblInfo.Text = sb.ToString();
    }

    // ---------- 绘图 ----------
    private float Angle => float.TryParse(cmbRotate.Text, out var a) ? a : 0;

    private Bitmap RenderLabel(float wMm, float hMm, string text, float fontPt, string fontFamily, float dpiX, float dpiY)
    {
        int pxW = Math.Max(1, (int)Math.Round(wMm / 25.4f * dpiX));
        int pxH = Math.Max(1, (int)Math.Round(hMm / 25.4f * dpiY));
        var bmp = new Bitmap(pxW, pxH);
        bmp.SetResolution(dpiX, dpiY);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        using (var pen = new Pen(Color.Black, Math.Max(1f, dpiX / 200f)))
            g.DrawRectangle(pen, 0, 0, pxW - 1, pxH - 1);

        using var font = new Font(fontFamily, fontPt, FontStyle.Regular, GraphicsUnit.Point);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, Brushes.Black, new RectangleF(0, 0, pxW, pxH), sf);
        return bmp;
    }

    private static Bitmap RotateBitmap(Bitmap src, float angle)
    {
        if (Math.Abs(angle) < 0.01f) return (Bitmap)src.Clone();
        var bmp = (Bitmap)src.Clone();
        switch ((int)Math.Round(angle))
        {
            case 90: bmp.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
            case 180: bmp.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
            case 270: bmp.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
        }
        return bmp;
    }

    private (float dpiX, float dpiY) GetPrinterDpi()
    {
        try
        {
            using var mg = _doc.PrinterSettings.CreateMeasurementGraphics();
            return (mg.DpiX, mg.DpiY);
        }
        catch
        {
            return (203f, 203f);
        }
    }

    private void UpdatePreview()
    {
        try
        {
            var (dpiX, dpiY) = GetPrinterDpi();
            float useDpi = Math.Max(dpiX, 200f);
            using var bmp = RenderLabel((float)numW.Value, (float)numH.Value, txtText.Text,
                (float)numFont.Value, cmbFont.Text, useDpi, useDpi);
            using var rot = RotateBitmap(bmp, Angle);
            var old = picPreview.Image;
            picPreview.Image = (Bitmap)rot.Clone();
            old?.Dispose();
        }
        catch (Exception ex)
        {
            lblInfo.Text = "预览失败: " + ex.Message;
        }
    }

    // ---------- 打印 ----------
    private void ApplySelectedPaper()
    {
        if (lstPapers.SelectedIndex >= 0 && lstPapers.SelectedIndex < _papers.Count)
            _doc.DefaultPageSettings.PaperSize = _papers[lstPapers.SelectedIndex];
    }

    private void ShowPrintSettings()
    {
        try
        {
            using var dlg = new PrintDialog
            {
                Document = _doc,
                AllowSomePages = false,
                AllowPrintToFile = false,
                UseEXDialog = true
            };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                RefreshPaperList();
                RefreshPrinterInfo();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "打开打印设置失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DoPrint()
    {
        if (cmbPrinter.SelectedItem is not string name)
        {
            MessageBox.Show(this, "请先选择打印机", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        ApplySelectedPaper();

        _curWmm = (float)numW.Value;
        _curHmm = (float)numH.Value;
        _curOffX = (float)numOffX.Value;
        _curOffY = (float)numOffY.Value;
        _curAngle = Angle;

        var (dpiX, dpiY) = GetPrinterDpi();
        using var baseBmp = RenderLabel(_curWmm, _curHmm, txtText.Text, (float)numFont.Value, cmbFont.Text, dpiX, dpiY);
        _currentBitmap?.Dispose();
        _currentBitmap = RotateBitmap(baseBmp, _curAngle);

        _doc.PrintPage -= OnPrintPage;
        _doc.PrintPage += OnPrintPage;
        _doc.PrintController = new StandardPrintController();

        try
        {
            _doc.Print();
            MessageBox.Show(this, "已发送到打印机。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "打印失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _currentBitmap?.Dispose();
            _currentBitmap = null;
        }
    }

    private void OnPrintPage(object sender, PrintPageEventArgs e)
    {
        var g = e.Graphics!;
        g.PageUnit = GraphicsUnit.Millimeter;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        float w = _curWmm, h = _curHmm;
        float bx = _curOffX, by = _curOffY, bw = w, bh = h;

        if (((int)Math.Round(_curAngle)) is 90 or 270)
        {
            // 旋转后位图尺寸为 高×宽，按比例居中放入标签区域，避免拉伸变形
            float rw = h, rh = w;
            float scale = Math.Min(w / rw, h / rh);
            bw = rw * scale; bh = rh * scale;
            bx = _curOffX + (w - bw) / 2f;
            by = _curOffY + (h - bh) / 2f;
        }

        if (_currentBitmap != null)
            g.DrawImage(_currentBitmap, new RectangleF(bx, by, bw, bh));
        e.HasMorePages = false;
    }
}
