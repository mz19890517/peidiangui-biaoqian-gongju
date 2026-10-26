using System.Drawing.Printing;

namespace LabelTool;

/// <summary>设置窗口：三个选项卡 —— 常规（纸张/粘贴）、标签预设（独有参数+可选自定义）、打印机共用参数</summary>
public class SettingsForm : Form
{
    private readonly AppState _state;
    private readonly List<Preset> _working;
    private readonly List<PaperSize> _driverPapers = new();   // 预设绑定打印机的驱动纸张（自定义参数用）
    private readonly List<PaperSize> _shPapers = new();       // 共用参数打印机的驱动纸张

    // ---- 常规 ----
    private readonly ComboBox cmbPaperKind = new();
    private readonly NumericUpDown numPaperW = new();
    private readonly NumericUpDown numPaperH = new();
    private readonly TextBox txtMergeSep = new();

    // ---- 标签预设（独有参数 + 可选自定义参数） ----
    private readonly ListBox lstPresets = new();
    private readonly TextBox txtName = new();
    private readonly NumericUpDown numW = new();
    private readonly NumericUpDown numH = new();
    private readonly ComboBox cmbPrinter = new();
    private readonly CheckBox chkCustom = new();
    private readonly NumericUpDown numFont = new();
    private readonly ComboBox cmbFont = new();
    private readonly NumericUpDown numLineSpacing = new();
    private readonly NumericUpDown numCharSpacing = new();
    private readonly NumericUpDown numWeight = new();
    private readonly ComboBox cmbPaper = new();
    private readonly ComboBox cmbMode = new();
    private readonly ComboBox cmbRotation = new();
    private readonly ComboBox cmbHAlign = new();
    private readonly ComboBox cmbVAlign = new();

    // ---- 打印机共用参数 ----
    private readonly ComboBox cmbShPrinter = new();
    private readonly ComboBox cmbShPaper = new();
    private readonly ComboBox cmbShMode = new();
    private readonly ComboBox cmbShRotation = new();
    private readonly NumericUpDown numShFont = new();
    private readonly ComboBox cmbShFont = new();
    private readonly NumericUpDown numShLine = new();
    private readonly NumericUpDown numShChar = new();
    private readonly NumericUpDown numShWeight = new();
    private readonly ComboBox cmbShHAlign = new();
    private readonly ComboBox cmbShVAlign = new();
    private readonly TextBox txtAlias = new();
    private readonly NumericUpDown numPrinterOffX = new();
    private readonly NumericUpDown numPrinterOffY = new();

    private readonly Dictionary<string, string> _aliasWorking;
    private readonly Dictionary<string, PrintOffset> _offsetWorking;
    private readonly Dictionary<string, PrinterSharedParams> _sharedWorking;
    private string? _aliasPrinter;   // 打印机选项卡当前选中的真实打印机
    private bool _loading;

    private static readonly (string Name, double W, double H)[] PaperKinds =
    {
        ("A4 (210×297)", 210, 297),
        ("银贴纸 (100×500)", 100, 500),
        ("自定义", 0, 0),
    };

    public SettingsForm(AppState state)
    {
        _state = state;
        _working = state.Presets.Select(Clone).ToList();
        _aliasWorking = state.PrinterAliases != null ? new Dictionary<string, string>(state.PrinterAliases) : new();
        _offsetWorking = state.PrinterOffsets != null ? new Dictionary<string, PrintOffset>(state.PrinterOffsets) : new();
        _sharedWorking = state.PrinterSharedParams != null ? new Dictionary<string, PrinterSharedParams>(state.PrinterSharedParams.ToDictionary(kv => kv.Key, kv => CloneShared(kv.Value))) : new();
        Text = "设置";
        ClientSize = new Size(780, 656);
        Font = new Font("微软雅黑", 9F);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        BuildUi();

        _loading = true;
        txtMergeSep.Text = state.MergeSeparator ?? " ";
        LoadPaper();
        ReloadPrinters();      // 预设绑定打印机下拉
        InitSharedPrinters();  // 打印机共用参数下拉（选中第一台并载入其参数）
        _loading = false;

        RefreshList();
    }

    private static Preset Clone(Preset p) => new()
    {
        Name = p.Name, WidthMm = p.WidthMm, HeightMm = p.HeightMm,
        PrinterName = p.PrinterName,
        UseCustomParams = p.UseCustomParams,
        FontPt = p.FontPt, FontFamily = p.FontFamily,
        LineSpacing = p.LineSpacing, CharSpacing = p.CharSpacing, Weight = p.Weight,
        PrintMode = p.PrintMode, PaperName = p.PaperName,
        Rotation = p.Rotation, OffsetX = p.OffsetX, OffsetY = p.OffsetY,
        HAlign = p.HAlign, VAlign = p.VAlign
    };

    private static PrinterSharedParams CloneShared(PrinterSharedParams s) => new()
    {
        FontPt = s.FontPt, FontFamily = s.FontFamily,
        LineSpacing = s.LineSpacing, CharSpacing = s.CharSpacing, Weight = s.Weight,
        PrintMode = s.PrintMode, PaperName = s.PaperName,
        Rotation = s.Rotation,
        HAlign = s.HAlign, VAlign = s.VAlign
    };

    // ================= 界面 =================
    private void BuildUi()
    {
        var tabs = new TabControl { Left = 8, Top = 8, Width = 764, Height = 584 };
        var tabCommon = new TabPage { Text = "常规", Padding = new Padding(10, 10, 10, 10) };
        var tabPreset = new TabPage { Text = "标签预设", Padding = new Padding(10, 10, 10, 10) };
        var tabShared = new TabPage { Text = "打印机共用参数", Padding = new Padding(10, 10, 10, 10) };
        tabs.TabPages.AddRange(new[] { tabCommon, tabPreset, tabShared });

        BuildCommonTab(tabCommon);
        BuildPresetTab(tabPreset);
        BuildSharedTab(tabShared);

        var btnOk = new Button { Text = "确定", Left = 556, Top = 600, Width = 108, Height = 32, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "取消", Left = 672, Top = 600, Width = 92, Height = 32, DialogResult = DialogResult.Cancel };
        btnOk.Click += (_, _) => Commit();
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange(new Control[] { tabs, btnOk, btnCancel });
    }

    private void BuildCommonTab(TabPage page)
    {
        var grpPaper = new GroupBox { Text = "纸张设置", Left = 12, Top = 12, Width = 360, Height = 110 };
        grpPaper.Controls.Add(new Label { Text = "纸张类型:", Left = 12, Top = 30, AutoSize = true });
        cmbPaperKind.Left = 90; cmbPaperKind.Top = 26; cmbPaperKind.Width = 220;
        cmbPaperKind.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var k in PaperKinds) cmbPaperKind.Items.Add(k.Name);
        cmbPaperKind.SelectedIndexChanged += (_, _) => OnPaperKindChanged();
        grpPaper.Controls.Add(cmbPaperKind);
        grpPaper.Controls.Add(new Label { Text = "宽(mm):", Left = 12, Top = 66, AutoSize = true });
        SetupNum(numPaperW, 90, 62, 1, 3000, 210, 1, 1); grpPaper.Controls.Add(numPaperW);
        grpPaper.Controls.Add(new Label { Text = "高(mm):", Left = 185, Top = 66, AutoSize = true });
        SetupNum(numPaperH, 245, 62, 1, 3000, 297, 1, 1); grpPaper.Controls.Add(numPaperH);

        var grpPaste = new GroupBox { Text = "粘贴设置", Left = 384, Top = 12, Width = 350, Height = 110 };
        grpPaste.Controls.Add(new Label { Text = "合并连接符:", Left = 12, Top = 34, AutoSize = true });
        txtMergeSep.Left = 100; txtMergeSep.Top = 30; txtMergeSep.Width = 220;
        grpPaste.Controls.Add(txtMergeSep);
        grpPaste.Controls.Add(new Label { Text = "二合一/三合一粘贴连接多行的符号（默认一个空格）", Left = 12, Top = 66, AutoSize = true, ForeColor = Color.DimGray });

        page.Controls.AddRange(new Control[] { grpPaper, grpPaste });
    }

    private void BuildPresetTab(TabPage page)
    {
        lstPresets.Left = 8; lstPresets.Top = 12; lstPresets.Width = 240; lstPresets.Height = 490;
        lstPresets.IntegralHeight = false;
        lstPresets.SelectedIndexChanged += (_, _) => LoadPresetToFields();
        page.Controls.Add(lstPresets);

        int x0 = 264, vx = 342, lx2 = 512, vx2 = 582;
        page.Controls.Add(new Label { Text = "名称:", Left = x0, Top = 18, AutoSize = true });
        txtName.Left = vx; txtName.Top = 14; txtName.Width = 150;
        page.Controls.Add(txtName);

        page.Controls.Add(new Label { Text = "宽(mm):", Left = x0, Top = 50, AutoSize = true });
        SetupNum(numW, vx, 46, 1, 2000, 50, 1, 0.5m); page.Controls.Add(numW);
        page.Controls.Add(new Label { Text = "高(mm):", Left = lx2, Top = 50, AutoSize = true });
        SetupNum(numH, vx2, 46, 1, 2000, 14, 1, 0.5m); page.Controls.Add(numH);

        page.Controls.Add(new Label { Text = "绑定打印机:", Left = x0, Top = 82, AutoSize = true });
        cmbPrinter.Left = vx; cmbPrinter.Top = 78; cmbPrinter.Width = 350;
        cmbPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPrinter.SelectedIndexChanged += (_, _) => OnPresetPrinterChanged();
        page.Controls.Add(cmbPrinter);

        page.Controls.Add(new Label { Text = "以上三项为标签/预设独有；其余参数在“打印机共用参数”页设置，", Left = vx, Top = 112, AutoSize = true, ForeColor = Color.DimGray });
        page.Controls.Add(new Label { Text = "或勾选下方自定义后单独设置。", Left = vx, Top = 130, AutoSize = true, ForeColor = Color.DimGray });

        chkCustom.Text = "本标签自定义参数（仅此预设生效）";
        chkCustom.Left = x0; chkCustom.Top = 152; chkCustom.Width = 300;
        chkCustom.CheckedChanged += (_, _) => UpdateCustomState();
        page.Controls.Add(chkCustom);

        // 自定义参数区（未勾选时禁用并变灰）
        page.Controls.Add(new Label { Text = "字号(pt):", Left = x0, Top = 186, AutoSize = true });
        SetupNum(numFont, vx, 182, 1, 300, 13, 1, 1); page.Controls.Add(numFont);
        page.Controls.Add(new Label { Text = "字体:", Left = lx2, Top = 186, AutoSize = true });
        cmbFont.Left = vx2; cmbFont.Top = 182; cmbFont.Width = 120;
        cmbFont.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbFont.Items.AddRange(new object[] { "宋体", "黑体", "微软雅黑", "楷体" });
        cmbFont.SelectedIndex = 0;
        page.Controls.Add(cmbFont);

        page.Controls.Add(new Label { Text = "驱动纸张:", Left = x0, Top = 218, AutoSize = true });
        cmbPaper.Left = vx; cmbPaper.Top = 214; cmbPaper.Width = 350;
        cmbPaper.DropDownStyle = ComboBoxStyle.DropDownList;
        page.Controls.Add(cmbPaper);

        page.Controls.Add(new Label { Text = "打印模式:", Left = x0, Top = 250, AutoSize = true });
        cmbMode.Left = vx; cmbMode.Top = 246; cmbMode.Width = 160;
        cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbMode.Items.AddRange(new object[] { "整版（一张纸多个标签）", "单标签（一个标签一页）" });
        cmbMode.SelectedIndex = 0;
        page.Controls.Add(cmbMode);
        page.Controls.Add(new Label { Text = "旋转:", Left = lx2 + 30, Top = 250, AutoSize = true });
        cmbRotation.Left = vx2 + 20; cmbRotation.Top = 246; cmbRotation.Width = 90;
        cmbRotation.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbRotation.Items.AddRange(new object[] { "0", "90", "180", "270" });
        cmbRotation.SelectedIndex = 0;
        page.Controls.Add(cmbRotation);

        page.Controls.Add(new Label { Text = "行距(mm):", Left = x0, Top = 282, AutoSize = true });
        SetupNum(numLineSpacing, vx, 278, -10m, 20m, 0, 2, 0.1m); page.Controls.Add(numLineSpacing);
        page.Controls.Add(new Label { Text = "字距(mm):", Left = lx2, Top = 282, AutoSize = true });
        SetupNum(numCharSpacing, vx2, 278, -5m, 30m, 0, 2, 0.1m); page.Controls.Add(numCharSpacing);
        page.Controls.Add(new Label { Text = "粗细(mm):", Left = x0, Top = 314, AutoSize = true });
        SetupNum(numWeight, vx, 310, 0m, 1.5m, 0, 2, 0.05m); page.Controls.Add(numWeight);

        page.Controls.Add(new Label { Text = "水平对齐:", Left = x0, Top = 346, AutoSize = true });
        cmbHAlign.Left = vx; cmbHAlign.Top = 342; cmbHAlign.Width = 110;
        cmbHAlign.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbHAlign.Items.AddRange(new object[] { "居中", "左对齐", "右对齐" });
        cmbHAlign.SelectedIndex = 0;
        page.Controls.Add(cmbHAlign);
        page.Controls.Add(new Label { Text = "垂直对齐:", Left = lx2, Top = 346, AutoSize = true });
        cmbVAlign.Left = vx2; cmbVAlign.Top = 342; cmbVAlign.Width = 110;
        cmbVAlign.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbVAlign.Items.AddRange(new object[] { "居中", "上对齐", "下对齐" });
        cmbVAlign.SelectedIndex = 0;
        page.Controls.Add(cmbVAlign);

        var btnAdd = new Button { Text = "新增", Left = x0, Top = 386, Width = 100, Height = 30 };
        var btnApply = new Button { Text = "应用到所选", Left = x0 + 110, Top = 386, Width = 110, Height = 30 };
        var btnDelete = new Button { Text = "删除", Left = x0 + 230, Top = 386, Width = 100, Height = 30 };
        var btnUp = new Button { Text = "上移", Left = x0 + 350, Top = 386, Width = 80, Height = 30 };
        var btnDown = new Button { Text = "下移", Left = x0 + 440, Top = 386, Width = 80, Height = 30 };
        btnAdd.Click += (_, _) => AddPreset();
        btnApply.Click += (_, _) => ApplyToSelected();
        btnDelete.Click += (_, _) => DeleteSelected();
        btnUp.Click += (_, _) => MoveSelected(-1);
        btnDown.Click += (_, _) => MoveSelected(1);
        page.Controls.AddRange(new Control[] { btnAdd, btnApply, btnDelete, btnUp, btnDown });
    }

    private void BuildSharedTab(TabPage page)
    {
        var grp = new GroupBox { Text = "该打印机的共用排版参数（对绑定它的所有“未自定义”预设生效）", Left = 12, Top = 96, Width = 720, Height = 250 };

        grp.Controls.Add(new Label { Text = "驱动纸张:", Left = 12, Top = 38, AutoSize = true });
        cmbShPaper.Left = 100; cmbShPaper.Top = 34; cmbShPaper.Width = 300;
        cmbShPaper.DropDownStyle = ComboBoxStyle.DropDownList;
        grp.Controls.Add(cmbShPaper);

        grp.Controls.Add(new Label { Text = "打印模式:", Left = 420, Top = 38, AutoSize = true });
        cmbShMode.Left = 500; cmbShMode.Top = 34; cmbShMode.Width = 200;
        cmbShMode.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbShMode.Items.AddRange(new object[] { "整版（一张纸多个标签）", "单标签（一个标签一页）" });
        cmbShMode.SelectedIndex = 0;
        grp.Controls.Add(cmbShMode);

        grp.Controls.Add(new Label { Text = "字号(pt):", Left = 12, Top = 76, AutoSize = true });
        SetupNum(numShFont, 100, 72, 1, 300, 13, 1, 1); grp.Controls.Add(numShFont);
        grp.Controls.Add(new Label { Text = "字体:", Left = 250, Top = 76, AutoSize = true });
        cmbShFont.Left = 310; cmbShFont.Top = 72; cmbShFont.Width = 120;
        cmbShFont.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbShFont.Items.AddRange(new object[] { "宋体", "黑体", "微软雅黑", "楷体" });
        cmbShFont.SelectedIndex = 0;
        grp.Controls.Add(cmbShFont);
        grp.Controls.Add(new Label { Text = "旋转:", Left = 470, Top = 76, AutoSize = true });
        cmbShRotation.Left = 530; cmbShRotation.Top = 72; cmbShRotation.Width = 90;
        cmbShRotation.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbShRotation.Items.AddRange(new object[] { "0", "90", "180", "270" });
        cmbShRotation.SelectedIndex = 0;
        grp.Controls.Add(cmbShRotation);

        grp.Controls.Add(new Label { Text = "行距(mm):", Left = 12, Top = 116, AutoSize = true });
        SetupNum(numShLine, 100, 112, -10m, 20m, 0, 2, 0.1m); grp.Controls.Add(numShLine);
        grp.Controls.Add(new Label { Text = "字距(mm):", Left = 250, Top = 116, AutoSize = true });
        SetupNum(numShChar, 310, 112, -5m, 30m, 0, 2, 0.1m); grp.Controls.Add(numShChar);
        grp.Controls.Add(new Label { Text = "粗细(mm):", Left = 470, Top = 116, AutoSize = true });
        SetupNum(numShWeight, 540, 112, 0m, 1.5m, 0, 2, 0.05m); numShWeight.Width = 80; grp.Controls.Add(numShWeight);

        grp.Controls.Add(new Label { Text = "水平对齐:", Left = 12, Top = 156, AutoSize = true });
        cmbShHAlign.Left = 100; cmbShHAlign.Top = 152; cmbShHAlign.Width = 110;
        cmbShHAlign.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbShHAlign.Items.AddRange(new object[] { "居中", "左对齐", "右对齐" });
        cmbShHAlign.SelectedIndex = 0;
        grp.Controls.Add(cmbShHAlign);
        grp.Controls.Add(new Label { Text = "垂直对齐:", Left = 250, Top = 156, AutoSize = true });
        cmbShVAlign.Left = 310; cmbShVAlign.Top = 152; cmbShVAlign.Width = 110;
        cmbShVAlign.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbShVAlign.Items.AddRange(new object[] { "居中", "上对齐", "下对齐" });
        cmbShVAlign.SelectedIndex = 0;
        grp.Controls.Add(cmbShVAlign);

        grp.Controls.Add(new Label { Text = "勾选了“本标签自定义参数”的预设不受这些共用参数影响", Left = 12, Top = 200, AutoSize = true, ForeColor = Color.DimGray });

        var grpOff = new GroupBox { Text = "别名与打印偏移（按打印机保存）", Left = 12, Top = 356, Width = 720, Height = 130 };
        grpOff.Controls.Add(new Label { Text = "别名:", Left = 12, Top = 32, AutoSize = true });
        txtAlias.Left = 100; txtAlias.Top = 28; txtAlias.Width = 180;
        grpOff.Controls.Add(txtAlias);
        grpOff.Controls.Add(new Label { Text = "（预览里显示的名字）", Left = 290, Top = 32, AutoSize = true, ForeColor = Color.DimGray });

        grpOff.Controls.Add(new Label { Text = "偏移X(mm):", Left = 12, Top = 70, AutoSize = true });
        SetupNum(numPrinterOffX, 100, 66, -100m, 100m, 0, 1, 0.5m); numPrinterOffX.Width = 80; grpOff.Controls.Add(numPrinterOffX);
        grpOff.Controls.Add(new Label { Text = "偏移Y(mm):", Left = 250, Top = 70, AutoSize = true });
        SetupNum(numPrinterOffY, 310, 66, -100m, 100m, 0, 1, 0.5m); numPrinterOffY.Width = 80; grpOff.Controls.Add(numPrinterOffY);
        grpOff.Controls.Add(new Label { Text = "整页位置微调，例如惠普打印机把上/左内容裁掉时用它补偿", Left = 12, Top = 100, AutoSize = true, ForeColor = Color.DimGray });

        // 顶部打印机选择
        page.Controls.Add(new Label { Text = "打印机:", Left = 18, Top = 22, AutoSize = true });
        cmbShPrinter.Left = 90; cmbShPrinter.Top = 18; cmbShPrinter.Width = 320;
        cmbShPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbShPrinter.SelectedIndexChanged += (_, _) => OnSharedPrinterChanged();
        page.Controls.Add(cmbShPrinter);

        page.Controls.AddRange(new Control[] { grp, grpOff });
    }

    private static void SetupNum(NumericUpDown n, int left, int top, decimal min, decimal max, decimal val, int decimals, decimal step)
    {
        n.Left = left; n.Top = top; n.Width = 90;
        n.Minimum = min; n.Maximum = max; n.Value = val; n.DecimalPlaces = decimals; n.Increment = step;
    }

    // ================= 数据/逻辑 =================
    private void LoadPaper()
    {
        double w = _state.Paper.WidthMm, h = _state.Paper.HeightMm;
        int idx = 2;
        for (int i = 0; i < PaperKinds.Length; i++)
            if (PaperKinds[i].Name != "自定义" && Math.Abs(PaperKinds[i].W - w) < 0.5 && Math.Abs(PaperKinds[i].H - h) < 0.5) { idx = i; break; }
        cmbPaperKind.SelectedIndex = idx;
        numPaperW.Value = (decimal)w;
        numPaperH.Value = (decimal)h;
    }

    private void OnPaperKindChanged()
    {
        int i = cmbPaperKind.SelectedIndex;
        if (i >= 0 && i < PaperKinds.Length && PaperKinds[i].Name != "自定义")
        {
            numPaperW.Value = (decimal)PaperKinds[i].W;
            numPaperH.Value = (decimal)PaperKinds[i].H;
        }
    }

    private void ReloadPrinters()
    {
        cmbPrinter.Items.Clear();
        cmbPrinter.Items.Add("（不绑定）");
        foreach (var p in PrintService.GetPrinters()) cmbPrinter.Items.Add(p);
        cmbPrinter.SelectedIndex = 0;
        RefreshPapers();
    }

    private void InitSharedPrinters()
    {
        cmbShPrinter.Items.Clear();
        foreach (var p in PrintService.GetPrinters()) cmbShPrinter.Items.Add(p);
        if (cmbShPrinter.Items.Count > 0)
        {
            // 构造期间 _loading=true，SelectedIndexChanged 会被守卫拦下，需手动初始化
            cmbShPrinter.SelectedIndex = 0;
            _aliasPrinter = cmbShPrinter.SelectedItem as string;
        }
        else
        {
            _aliasPrinter = null;
        }
        LoadShPrinterState();
    }

    /// <summary>预设绑定打印机变化：重建自定义纸张列表并按需刷新自定义状态</summary>
    private void OnPresetPrinterChanged()
    {
        if (_loading) return;
        RefreshPapers();
        if (chkCustom.Checked) LoadCustomPaperSelection();
    }

    /// <summary>共用参数页切换打印机：保存上一台的别名/偏移/共用参数，再载入新打印机信息</summary>
    private void OnSharedPrinterChanged()
    {
        if (_loading) return;
        SaveAliasIfNeeded();
        SaveSharedToWorking();
        _aliasPrinter = cmbShPrinter.SelectedItem as string;
        LoadShPrinterState();
    }

    private void LoadShPrinterState()
    {
        bool has = !string.IsNullOrEmpty(_aliasPrinter);
        txtAlias.Enabled = numPrinterOffX.Enabled = numPrinterOffY.Enabled = has;
        txtAlias.Text = (has && _aliasWorking.TryGetValue(_aliasPrinter!, out var a)) ? a : "";
        var off = has ? GetOffset(_aliasPrinter!) : null;
        numPrinterOffX.Value = off == null ? 0 : Clamp(off.X, numPrinterOffX);
        numPrinterOffY.Value = off == null ? 0 : Clamp(off.Y, numPrinterOffY);

        RefreshShPapers();
        if (has)
        {
            var s = GetSharedWorking(_aliasPrinter!);
            numShFont.Value = Clamp(s.FontPt <= 0 ? 11 : s.FontPt, numShFont);
            if (cmbShFont.Items.Contains(s.FontFamily)) cmbShFont.SelectedItem = s.FontFamily;
            else if (cmbShFont.Items.Count > 0) cmbShFont.SelectedIndex = 0;
            numShLine.Value = Clamp(s.LineSpacing, numShLine);
            numShChar.Value = Clamp(s.CharSpacing, numShChar);
            numShWeight.Value = Clamp(s.Weight, numShWeight);
            cmbShMode.SelectedIndex = s.PrintMode == "Single" ? 1 : 0;
            SelectPaper(cmbShPaper, _shPapers, s.PaperName);
            cmbShRotation.SelectedItem = ((int)Math.Round(s.Rotation)).ToString();
            if (cmbShRotation.SelectedItem == null) cmbShRotation.SelectedIndex = 0;
            cmbShHAlign.SelectedIndex = HAlignIndex(s.HAlign);
            cmbShVAlign.SelectedIndex = VAlignIndex(s.VAlign);
        }
    }

    private void RefreshPapers()
    {
        _driverPapers.Clear();
        cmbPaper.Items.Clear();
        cmbPaper.Items.Add("（驱动默认）");
        if (cmbPrinter.SelectedIndex > 0 && cmbPrinter.SelectedItem is string name)
        {
            foreach (var ps in PrintService.GetPapers(name)) _driverPapers.Add(ps);
            for (int k = 0; k < _driverPapers.Count; k++)
            {
                var ps = _driverPapers[k];
                double w = ps.Width / 100.0 * 25.4, h = ps.Height / 100.0 * 25.4;
                cmbPaper.Items.Add($"{ps.PaperName}  {w:0.#}×{h:0.#}mm");
            }
        }
        cmbPaper.SelectedIndex = 0;
    }

    private void RefreshShPapers()
    {
        _shPapers.Clear();
        cmbShPaper.Items.Clear();
        cmbShPaper.Items.Add("（驱动默认）");
        if (!string.IsNullOrEmpty(_aliasPrinter))
        {
            foreach (var ps in PrintService.GetPapers(_aliasPrinter)) _shPapers.Add(ps);
            for (int k = 0; k < _shPapers.Count; k++)
            {
                var ps = _shPapers[k];
                double w = ps.Width / 100.0 * 25.4, h = ps.Height / 100.0 * 25.4;
                cmbShPaper.Items.Add($"{ps.PaperName}  {w:0.#}×{h:0.#}mm");
            }
        }
        cmbShPaper.SelectedIndex = 0;
    }

    private static void SelectPaper(ComboBox cmb, List<PaperSize> list, string? paperName)
    {
        cmb.SelectedIndex = 0;
        if (!string.IsNullOrEmpty(paperName))
        {
            for (int k = 0; k < list.Count; k++)
                if (list[k].PaperName == paperName) { cmb.SelectedIndex = k + 1; break; }
        }
    }

    private void LoadCustomPaperSelection()
    {
        int i = lstPresets.SelectedIndex;
        if (i < 0 || i >= _working.Count) return;
        SelectPaper(cmbPaper, _driverPapers, _working[i].PaperName);
    }

    private void SaveAliasIfNeeded()
    {
        if (_loading || string.IsNullOrEmpty(_aliasPrinter)) return;
        string v = txtAlias.Text.Trim();
        if (v.Length > 0) _aliasWorking[_aliasPrinter] = v;
        else _aliasWorking.Remove(_aliasPrinter);

        var off = new PrintOffset { X = (double)numPrinterOffX.Value, Y = (double)numPrinterOffY.Value };
        if (Math.Abs(off.X) > 0.001 || Math.Abs(off.Y) > 0.001) _offsetWorking[_aliasPrinter] = off;
        else _offsetWorking.Remove(_aliasPrinter);
    }

    private PrintOffset? GetOffset(string printer) =>
        _offsetWorking.TryGetValue(printer, out var o) ? o : null;

    /// <summary>取（或建）某打印机的共用参数工作副本</summary>
    private PrinterSharedParams GetSharedWorking(string printer)
    {
        if (string.IsNullOrEmpty(printer)) return new PrinterSharedParams();
        if (!_sharedWorking.TryGetValue(printer, out var s))
        {
            s = new PrinterSharedParams();
            _sharedWorking[printer] = s;
        }
        return s;
    }

    /// <summary>把共用参数页当前数值写入当前打印机的共用设置</summary>
    private void SaveSharedToWorking()
    {
        if (_loading || string.IsNullOrEmpty(_aliasPrinter)) return;
        var s = GetSharedWorking(_aliasPrinter);
        s.FontPt = (double)numShFont.Value;
        s.FontFamily = cmbShFont.Text;
        s.LineSpacing = (double)numShLine.Value;
        s.CharSpacing = (double)numShChar.Value;
        s.Weight = (double)numShWeight.Value;
        s.PrintMode = cmbShMode.SelectedIndex == 1 ? "Single" : "Sheet";
        s.PaperName = SelShPaperName();
        s.Rotation = double.TryParse(cmbShRotation.Text, out var r) ? r : 0;
        s.HAlign = HAlignFromIndex(cmbShHAlign.SelectedIndex);
        s.VAlign = VAlignFromIndex(cmbShVAlign.SelectedIndex);
    }

    private void RefreshList()
    {
        int sel = lstPresets.SelectedIndex;
        lstPresets.Items.Clear();
        foreach (var p in _working) lstPresets.Items.Add(p.Name);
        if (_working.Count > 0) lstPresets.SelectedIndex = Math.Min(Math.Max(sel, 0), _working.Count - 1);
    }

    private void LoadPresetToFields()
    {
        int i = lstPresets.SelectedIndex;
        if (i < 0 || i >= _working.Count) return;
        var p = _working[i];
        _loading = true;
        try
        {
            txtName.Text = p.Name;
            numW.Value = Clamp(p.WidthMm, numW);
            numH.Value = Clamp(p.HeightMm, numH);
            chkCustom.Checked = p.UseCustomParams;

            cmbPrinter.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(p.PrinterName) && cmbPrinter.Items.Contains(p.PrinterName))
                cmbPrinter.SelectedItem = p.PrinterName;

            RefreshPapers();

            if (p.UseCustomParams)
            {
                numFont.Value = Clamp(p.FontPt <= 0 ? 11 : p.FontPt, numFont);
                if (cmbFont.Items.Contains(p.FontFamily)) cmbFont.SelectedItem = p.FontFamily;
                else if (cmbFont.Items.Count > 0) cmbFont.SelectedIndex = 0;
                numLineSpacing.Value = Clamp(p.LineSpacing, numLineSpacing);
                numCharSpacing.Value = Clamp(p.CharSpacing, numCharSpacing);
                numWeight.Value = Clamp(p.Weight, numWeight);
                cmbHAlign.SelectedIndex = HAlignIndex(p.HAlign);
                cmbVAlign.SelectedIndex = VAlignIndex(p.VAlign);
                cmbRotation.SelectedItem = ((int)Math.Round(p.Rotation)).ToString();
                if (cmbRotation.SelectedItem == null) cmbRotation.SelectedIndex = 0;
                SelectPaper(cmbPaper, _driverPapers, p.PaperName);
                cmbMode.SelectedIndex = p.PrintMode == "Single" ? 1 : 0;
            }
        }
        finally
        {
            _loading = false;
        }
        UpdateCustomState();
    }

    /// <summary>按“本标签自定义参数”勾选状态启用/禁用自定义参数控件</summary>
    private void UpdateCustomState()
    {
        bool custom = chkCustom.Checked;
        bool hasPrinter = cmbPrinter.SelectedIndex > 0 && cmbPrinter.SelectedItem is string;
        numFont.Enabled = custom;
        cmbFont.Enabled = custom;
        numLineSpacing.Enabled = custom;
        numCharSpacing.Enabled = custom;
        numWeight.Enabled = custom;
        cmbHAlign.Enabled = custom;
        cmbVAlign.Enabled = custom;
        cmbRotation.Enabled = custom;
        cmbPaper.Enabled = custom && hasPrinter;
        cmbMode.Enabled = custom && hasPrinter;
        if (custom && !_loading && hasPrinter) LoadCustomPaperSelection();
    }

    private static decimal Clamp(double v, NumericUpDown n)
    {
        if ((decimal)v < n.Minimum) return n.Minimum;
        if ((decimal)v > n.Maximum) return n.Maximum;
        return (decimal)v;
    }

    private string? SelPaperName()
    {
        int i = cmbPaper.SelectedIndex - 1;
        if (i >= 0 && i < _driverPapers.Count) return _driverPapers[i].PaperName;
        return null;
    }

    private string? SelShPaperName()
    {
        int i = cmbShPaper.SelectedIndex - 1;
        if (i >= 0 && i < _shPapers.Count) return _shPapers[i].PaperName;
        return null;
    }

    private void AddPreset()
    {
        string name = txtName.Text.Trim();
        if (name.Length == 0) { MessageBox.Show(this, "请输入预设名称", "提示"); return; }
        if (_working.Any(p => p.Name == name)) { MessageBox.Show(this, "名称已存在", "提示"); return; }
        _working.Add(ReadFields(name, null));
        RefreshList();
        lstPresets.SelectedIndex = _working.Count - 1;
    }

    private void ApplyToSelected()
    {
        int i = lstPresets.SelectedIndex;
        if (i < 0) { MessageBox.Show(this, "请先选择预设", "提示"); return; }
        string name = txtName.Text.Trim();
        if (name.Length == 0) { MessageBox.Show(this, "请输入预设名称", "提示"); return; }
        _working[i] = ReadFields(name, _working[i]);
        RefreshList();
        lstPresets.SelectedIndex = i;
    }

    private void DeleteSelected()
    {
        int i = lstPresets.SelectedIndex;
        if (i < 0) return;
        _working.RemoveAt(i);
        RefreshList();
    }

    private void MoveSelected(int dir)
    {
        int i = lstPresets.SelectedIndex;
        int j = i + dir;
        if (i < 0 || j < 0 || j >= _working.Count) return;
        (_working[i], _working[j]) = (_working[j], _working[i]);
        RefreshList();
        lstPresets.SelectedIndex = j;
    }

    /// <summary>从界面读取预设。original：非自定义且未绑定打印机时保留原快照值</summary>
    private Preset ReadFields(string name, Preset? original)
    {
        var pn = cmbPrinter.SelectedIndex > 0 ? cmbPrinter.SelectedItem as string : null;
        var p = new Preset
        {
            Name = name,
            WidthMm = (double)numW.Value,
            HeightMm = (double)numH.Value,
            PrinterName = pn,
            UseCustomParams = chkCustom.Checked,
        };
        if (p.UseCustomParams)
        {
            p.FontPt = (double)numFont.Value;
            p.FontFamily = cmbFont.Text;
            p.LineSpacing = (double)numLineSpacing.Value;
            p.CharSpacing = (double)numCharSpacing.Value;
            p.Weight = (double)numWeight.Value;
            p.PaperName = SelPaperName();
            p.PrintMode = cmbMode.SelectedIndex == 1 ? "Single" : "Sheet";
            p.Rotation = double.TryParse(cmbRotation.Text, out var r) ? r : 0;
            p.HAlign = HAlignFromIndex(cmbHAlign.SelectedIndex);
            p.VAlign = VAlignFromIndex(cmbVAlign.SelectedIndex);
        }
        else if (!string.IsNullOrEmpty(pn))
        {
            var s = GetSharedWorking(pn);
            p.FontPt = s.FontPt;
            p.FontFamily = s.FontFamily;
            p.LineSpacing = s.LineSpacing;
            p.CharSpacing = s.CharSpacing;
            p.Weight = s.Weight;
            p.PaperName = s.PaperName;
            p.PrintMode = s.PrintMode;
            p.Rotation = s.Rotation;
            p.HAlign = s.HAlign;
            p.VAlign = s.VAlign;
        }
        else if (original != null)
        {
            p.FontPt = original.FontPt;
            p.FontFamily = original.FontFamily;
            p.LineSpacing = original.LineSpacing;
            p.CharSpacing = original.CharSpacing;
            p.Weight = original.Weight;
            p.PaperName = original.PaperName;
            p.PrintMode = original.PrintMode;
            p.Rotation = original.Rotation;
            p.HAlign = original.HAlign;
            p.VAlign = original.VAlign;
        }
        return p;
    }

    private static int HAlignIndex(string? a) => a == "Left" ? 1 : a == "Right" ? 2 : 0;
    private static int VAlignIndex(string? a) => a == "Top" ? 1 : a == "Bottom" ? 2 : 0;
    private static string HAlignFromIndex(int i) => i == 1 ? "Left" : i == 2 ? "Right" : "Center";
    private static string VAlignFromIndex(int i) => i == 1 ? "Top" : i == 2 ? "Bottom" : "Middle";

    private void Commit()
    {
        SaveAliasIfNeeded();
        SaveSharedToWorking();
        // 把当前编辑中的字段应用回所选预设，避免改了参数点“确定”却没生效
        int i = lstPresets.SelectedIndex;
        if (i >= 0 && i < _working.Count)
        {
            string name = txtName.Text.Trim();
            if (name.Length == 0) name = _working[i].Name;
            else if (name != _working[i].Name && _working.Any(p => p.Name == name)) name = _working[i].Name;
            _working[i] = ReadFields(name, _working[i]);
        }
        _state.Paper.WidthMm = (double)numPaperW.Value;
        _state.Paper.HeightMm = (double)numPaperH.Value;
        _state.Paper.Name = cmbPaperKind.SelectedItem as string ?? "自定义";
        _state.Presets = _working;
        _state.MergeSeparator = txtMergeSep.Text;
        _state.PrinterAliases = new Dictionary<string, string>(_aliasWorking);
        _state.PrinterOffsets = new Dictionary<string, PrintOffset>(_offsetWorking);
        if (_state.PrinterSharedParams == null) _state.PrinterSharedParams = new();
        foreach (var kv in _sharedWorking)
            _state.PrinterSharedParams[kv.Key] = CloneShared(kv.Value);
    }
}
