using System.Drawing.Printing;

namespace LabelTool;

public class PrintUnit
{
    public Preset Preset { get; set; } = new();
    public string TypeKey { get; set; } = "";
    public List<string> Labels { get; set; } = new();
    public string? FontFamily { get; set; }
    public double FontSize { get; set; }
    public bool AutoFit { get; set; }
    public bool AutoSize { get; set; }
    public double LineSpacing { get; set; }
    public double CharSpacing { get; set; }
    public double Weight { get; set; }
}

public class SpecMeta
{
    public int Index { get; set; }
    public string Printer { get; set; } = "";
    public bool Single { get; set; }
    public string TypeKey { get; set; } = "";
    public int LabelIndex { get; set; } = -1;
    public bool IsCurrentType { get; set; }
    public Preset Preset { get; set; } = new();
    public string? FontFamily { get; set; }
    public double FontSize { get; set; }
    public bool AutoFit { get; set; }
    public bool AutoSize { get; set; }
    public double LineSpacing { get; set; }
    public double CharSpacing { get; set; }
    public double Weight { get; set; }
}

public class MainForm : Form
{
    private AppState _state;
    private PreviewForm? _preview;
    private List<PreviewSource> _lastSources = new();

    // 当前柜子按“标签类型”存储的内容
    private Dictionary<string, TypeData> _types = new();
    private string _currentTypeKey = "";

    private bool _loading;

    private readonly ComboBox cmbProject = new();
    private readonly ComboBox cmbCabinet = new();
    private readonly ComboBox cmbPreset = new();
    private readonly CheckBox chkRealtime = new();
    private readonly Label lblInfo = new();
    private readonly Label lblStatus = new();

    private readonly DataGridView grid = new();

    private readonly Stack<List<string[]>> _undo = new();
    private readonly Stack<List<string[]>> _redo = new();
    private List<string[]>? _editBefore;
    private readonly Button btnUndo = new();
    private readonly Button btnRedo = new();

    private static readonly Preset DefaultPreset = new()
    { Name = "默认", WidthMm = 50, HeightMm = 14, FontPt = 13, FontFamily = "宋体" };

    private const int ColSeq = 0;
    private const int ColDataStart = 1;

    public MainForm()
    {
        _state = Storage.Load();
        MigrateIfNeeded();
        EnsureDefaultOnStart();
        Text = "配电柜标签工具 v4";
        ClientSize = new Size(1120, 760);
        Font = new Font("微软雅黑", 9F);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        BuildUi();
        ReloadPresets();
        ReloadProjects();
        UpdateSeqAndStatus();
        FormClosing += (_, _) => { SaveCurrentCabinet(); Storage.Save(_state); };
    }

    private void MigrateIfNeeded()
    {
        if (_state.Projects.Count > 0) return;
        var proj = new ProjectInfo { Name = "默认项目" };
        proj.Cabinets.Add("默认柜子");
        _state.Projects.Add(proj);
        _state.CurrentProject = proj.Name;
        _state.CurrentCabinet = "默认柜子";
        Storage.SaveCabinet(new Cabinet { Project = proj.Name, Name = "默认柜子" });
    }

    /// <summary>每次启动都定位到“默认项目/默认柜子”，避免直接打开上次编辑的柜子</summary>
    private void EnsureDefaultOnStart()
    {
        var proj = _state.Projects.Find(p => p.Name == "默认项目");
        if (proj == null)
        {
            proj = new ProjectInfo { Name = "默认项目" };
            _state.Projects.Insert(0, proj);
        }
        if (!proj.Cabinets.Contains("默认柜子"))
        {
            proj.Cabinets.Insert(0, "默认柜子");
            Storage.SaveCabinet(new Cabinet { Project = "默认项目", Name = "默认柜子" });
        }
        _state.CurrentProject = "默认项目";
        _state.CurrentCabinet = "默认柜子";
    }

    // ============ 界面 ============
    private void BuildUi()
    {
        var topWrap = new Panel { Dock = DockStyle.Top, Height = 86 };
        var row1 = new Panel { Left = 0, Top = 0, Height = 42, Width = 1120 };
        var row2 = new Panel { Left = 0, Top = 42, Height = 42, Width = 1120 };
        topWrap.Controls.Add(row1);
        topWrap.Controls.Add(row2);

        // ---- 第一行：项目 / 柜子 ----
        row1.Controls.Add(new Label { Text = "项目:", Left = 8, Top = 14, AutoSize = true });
        cmbProject.Left = 52; cmbProject.Top = 9; cmbProject.Width = 200;
        cmbProject.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbProject.SelectedIndexChanged += (_, _) => OnProjectChanged();
        row1.Controls.Add(cmbProject);

        var btnNewProject = new Button { Text = "新建项目", Left = 260, Top = 8, Width = 90, Height = 28 };
        btnNewProject.Click += (_, _) => NewProject();
        row1.Controls.Add(btnNewProject);

        row1.Controls.Add(new Label { Text = "柜子:", Left = 366, Top = 14, AutoSize = true });
        cmbCabinet.Left = 410; cmbCabinet.Top = 9; cmbCabinet.Width = 200;
        cmbCabinet.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbCabinet.SelectedIndexChanged += (_, _) => OnCabinetChanged();
        row1.Controls.Add(cmbCabinet);

        var btnNewCabinet = new Button { Text = "新建柜子", Left = 618, Top = 8, Width = 90, Height = 28 };
        var btnSaveCabinet = new Button { Text = "保存柜子", Left = 714, Top = 8, Width = 90, Height = 28 };
        btnNewCabinet.Click += (_, _) => NewCabinet();
        btnSaveCabinet.Click += (_, _) => { SaveCurrentCabinet(); Storage.Save(_state); MessageBox.Show(this, "已保存当前柜子"); };
        row1.Controls.AddRange(new Control[] { btnNewCabinet, btnSaveCabinet });

        // ---- 第二行：标签类型 / 设置 / 预览 ----
        row2.Controls.Add(new Label { Text = "标签类型:", Left = 8, Top = 14, AutoSize = true });
        cmbPreset.Left = 70; cmbPreset.Top = 9; cmbPreset.Width = 200;
        cmbPreset.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPreset.SelectedIndexChanged += (_, _) => OnPresetChanged();
        row2.Controls.Add(cmbPreset);

        var btnSettings = new Button { Text = "设置...", Left = 280, Top = 8, Width = 80, Height = 28 };
        btnSettings.Click += (_, _) => OpenSettings();
        row2.Controls.Add(btnSettings);

        chkRealtime.Text = "实时预览"; chkRealtime.Left = 370; chkRealtime.Top = 12; chkRealtime.AutoSize = true;
        chkRealtime.Checked = true;
        chkRealtime.CheckedChanged += (_, _) => { if (chkRealtime.Checked) RefreshPreview(true); };
        row2.Controls.Add(chkRealtime);

        var btnPreview = new Button { Text = "打开预览", Left = 454, Top = 8, Width = 90, Height = 28 };
        btnPreview.Click += (_, _) => ShowPreview();
        row2.Controls.Add(btnPreview);

        var btnReload = new Button { Text = "重新载入", Left = 550, Top = 8, Width = 84, Height = 28 };
        btnReload.Click += (_, _) => { _state = Storage.Load(); MigrateIfNeeded(); ReloadPresets(); ReloadProjects(); RefreshPreview(true); };
        row2.Controls.Add(btnReload);

        lblInfo.Left = 642; lblInfo.Top = 14; lblInfo.AutoSize = true; lblInfo.ForeColor = Color.DimGray;
        row2.Controls.Add(lblInfo);

        var editBar = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(6) };
        var btnClear = new Button { Text = "清空表格", Left = 8, Top = 7, Width = 90, Height = 28 };
        btnUndo.Text = "撤销"; btnUndo.Left = 110; btnUndo.Top = 7; btnUndo.Width = 70; btnUndo.Height = 28;
        btnRedo.Text = "重做"; btnRedo.Left = 184; btnRedo.Top = 7; btnRedo.Width = 70; btnRedo.Height = 28;
        btnClear.Click += (_, _) => { PushUndo(); SetRows(new List<string[]>()); UpdateSeqAndStatus(); RefreshPreview(); };
        btnUndo.Click += (_, _) => Undo();
        btnRedo.Click += (_, _) => Redo();
        var btnPrintSettings = new Button { Text = "打印设置...", Left = 360, Top = 7, Width = 100, Height = 28 };
        btnPrintSettings.Click += (_, _) => ShowPrintSettings();
        lblStatus.Left = 480; lblStatus.Top = 13; lblStatus.AutoSize = true; lblStatus.ForeColor = Color.DimGray;
        editBar.Controls.AddRange(new Control[] { btnClear, btnUndo, btnRedo, btnPrintSettings, lblStatus });

        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = true;
        grid.RowHeadersVisible = false;
        grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "序号", Width = 54, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "第1行", Width = 200 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "第2行", Width = 200 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "第3行", Width = 200 });
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Columns[ColSeq].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        grid.Columns[ColSeq].Width = 54;
        grid.KeyDown += Grid_KeyDown;
        grid.CellBeginEdit += (_, _) => _editBefore = Snapshot();
        grid.CellEndEdit += (_, e) => OnCellEndEdit(e);
        grid.EditingControlShowing += Grid_EditingControlShowing;

        Controls.Add(grid);
        Controls.Add(editBar);
        Controls.Add(topWrap);

        BuildGridMenu();
        UpdateUndoButtons();
    }

    // ============ 项目 / 柜子 ============
    private void ReloadProjects()
    {
        _loading = true;
        cmbProject.Items.Clear();
        foreach (var p in _state.Projects) cmbProject.Items.Add(p.Name);
        int i = _state.Projects.FindIndex(p => p.Name == _state.CurrentProject);
        if (i < 0 && _state.Projects.Count > 0) { i = 0; _state.CurrentProject = _state.Projects[0].Name; }
        cmbProject.SelectedIndex = i;
        _loading = false;
        ReloadCabinets();
    }

    private void ReloadCabinets()
    {
        _loading = true;
        cmbCabinet.Items.Clear();
        var proj = _state.Projects.Find(p => p.Name == _state.CurrentProject);
        if (proj != null) foreach (var c in proj.Cabinets) cmbCabinet.Items.Add(c);
        int j = cmbCabinet.Items.IndexOf(_state.CurrentCabinet);
        if (j < 0 && cmbCabinet.Items.Count > 0) { j = 0; _state.CurrentCabinet = (string)cmbCabinet.Items[0]!; }
        cmbCabinet.SelectedIndex = j;
        _loading = false;
        OpenCabinet(_state.CurrentProject, _state.CurrentCabinet);
    }

    private void OnProjectChanged()
    {
        if (_loading) return;
        SaveCurrentCabinet();
        _state.CurrentProject = cmbProject.SelectedItem as string ?? "";
        _state.CurrentCabinet = "";
        ReloadCabinets();
        Storage.Save(_state);
    }

    private void OnCabinetChanged()
    {
        if (_loading) return;
        SaveCurrentCabinet();
        OpenCabinet(_state.CurrentProject, cmbCabinet.SelectedItem as string ?? "");
        Storage.Save(_state);
    }

    private void NewProject()
    {
        string? name = Prompt("项目名称：", "新建项目");
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        if (_state.Projects.Any(p => p.Name == name)) { MessageBox.Show(this, "项目已存在", "提示"); return; }
        SaveCurrentCabinet();
        _state.Projects.Add(new ProjectInfo { Name = name });
        _state.CurrentProject = name;
        _state.CurrentCabinet = "";
        Storage.Save(_state);
        ReloadProjects();
    }

    private void NewCabinet()
    {
        string? name = Prompt("柜子名称：", "新建柜子");
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        var proj = _state.Projects.Find(p => p.Name == _state.CurrentProject);
        if (proj == null) { MessageBox.Show(this, "请先新建项目", "提示"); return; }
        if (proj.Cabinets.Contains(name)) { MessageBox.Show(this, "柜子已存在", "提示"); return; }
        SaveCurrentCabinet();
        proj.Cabinets.Add(name);
        _state.CurrentCabinet = name;
        Storage.SaveCabinet(new Cabinet { Project = proj.Name, Name = name });
        Storage.Save(_state);
        ReloadCabinets();
    }

    private void OpenCabinet(string? project, string? name)
    {
        if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(name))
        {
            _types = new();
            _currentTypeKey = DefaultPreset.Name;
            SetRows(new List<string[]>());
            UpdateSeqAndStatus();
            RefreshPreview(true);
            return;
        }
        var cab = Storage.LoadCabinet(project, name);
        _state.CurrentProject = project;
        _state.CurrentCabinet = name;
        _types = cab.Types ?? new();

        string key = cab.CurrentType;
        if (PresetIndexOf(key) < 0) key = PresetIndexOf(_state.CurrentPresetName) >= 0 ? _state.CurrentPresetName : DefaultPreset.Name;
        _currentTypeKey = key;
        _loading = true;
        int idx = PresetIndexOf(key);
        if (idx >= 0 && idx < cmbPreset.Items.Count) cmbPreset.SelectedIndex = idx;
        _loading = false;
        UpdateInfo();
        LoadGridFromType();
        UpdateSeqAndStatus();
        RefreshPreview(true);
    }

    private void SaveCurrentCabinet()
    {
        if (string.IsNullOrEmpty(_state.CurrentProject) || string.IsNullOrEmpty(_state.CurrentCabinet)) return;
        SaveCurrentTypeContent();
        var cab = new Cabinet
        {
            Project = _state.CurrentProject,
            Name = _state.CurrentCabinet,
            Types = _types,
            CurrentType = _currentTypeKey
        };
        Storage.SaveCabinet(cab);
    }

    // ============ 标签类型（预设） ============
    private TypeData CurrentTypeData()
    {
        if (!_types.TryGetValue(_currentTypeKey, out var td)) { td = new TypeData(); _types[_currentTypeKey] = td; }
        return td;
    }

    /// <summary>把当前表格内容写回当前标签类型（内存）</summary>
    private void SaveCurrentTypeContent()
    {
        if (string.IsNullOrEmpty(_currentTypeKey)) return;
        CurrentTypeData().Labels = ReadGridLabels();
    }

    private void LoadGridFromType()
    {
        if (_types.TryGetValue(_currentTypeKey, out var td)) LoadLabelsToGrid(td.Labels);
        else LoadLabelsToGrid(new List<string>());
    }

    private void OnPresetChanged()
    {
        if (_loading) return;
        SaveCurrentTypeContent();
        _currentTypeKey = PresetKeyAt(cmbPreset.SelectedIndex);
        _state.CurrentPresetName = _currentTypeKey;
        LoadGridFromType();
        UpdateSeqAndStatus();
        UpdateInfo();
        SaveCurrentCabinet();
        Storage.Save(_state);
        RefreshPreview(true);
    }

    private void ReloadPresets()
    {
        int want = PresetIndexOf(_state.CurrentPresetName);
        if (want < 0) want = 0;
        _loading = true;
        cmbPreset.Items.Clear();
        cmbPreset.Items.Add("（默认）");
        foreach (var p in _state.Presets) cmbPreset.Items.Add(p.Name);
        if (want >= 0 && want < cmbPreset.Items.Count) cmbPreset.SelectedIndex = want;
        _loading = false;
        UpdateInfo();
    }

    private string PresetKeyAt(int index)
    {
        if (index <= 0) return DefaultPreset.Name;
        int i = index - 1;
        return (i >= 0 && i < _state.Presets.Count) ? _state.Presets[i].Name : DefaultPreset.Name;
    }

    private int PresetIndexOf(string? key)
    {
        if (string.IsNullOrEmpty(key)) return -1;
        int i = _state.Presets.FindIndex(p => p.Name == key);
        if (i >= 0) return i + 1;
        if (key == DefaultPreset.Name) return 0;
        return -1;
    }

    private Preset FindPreset(string key) => _state.Presets.Find(p => p.Name == key) ?? DefaultPreset;

    private Preset CurrentPreset => FindPreset(PresetKeyAt(cmbPreset.SelectedIndex));

    private PrinterSharedParams GetShared(string? printer)
    {
        if (string.IsNullOrWhiteSpace(printer)) return new PrinterSharedParams();
        if (_state.PrinterSharedParams != null && _state.PrinterSharedParams.TryGetValue(printer, out var s)) return s;
        return new PrinterSharedParams();
    }

    private PrinterSharedParams EnsureShared(string printer)
    {
        if (string.IsNullOrWhiteSpace(printer)) return new PrinterSharedParams();
        if (_state.PrinterSharedParams == null) _state.PrinterSharedParams = new();
        if (!_state.PrinterSharedParams.TryGetValue(printer, out var s))
        {
            s = new PrinterSharedParams();
            _state.PrinterSharedParams[printer] = s;
        }
        return s;
    }

    private sealed class Eff
    {
        public double FontPt = 11;
        public string FontFamily = "宋体";
        public double LineSpacing;
        public double CharSpacing;
        public double Weight;
        public string PrintMode = "Sheet";
        public string? PaperName;
        public double Rotation;
        public string HAlign = "Center";
        public string VAlign = "Middle";
    }

    private Eff ApplyEffective(Preset p)
    {
        var e = new Eff();
        if (!p.UseCustomParams && !string.IsNullOrWhiteSpace(p.PrinterName))
        {
            var sh = GetShared(p.PrinterName);
            e.FontPt = sh.FontPt <= 0 ? 11 : sh.FontPt;
            e.FontFamily = string.IsNullOrEmpty(sh.FontFamily) ? (string.IsNullOrEmpty(p.FontFamily) ? "宋体" : p.FontFamily) : sh.FontFamily;
            e.LineSpacing = sh.LineSpacing;
            e.CharSpacing = sh.CharSpacing;
            e.Weight = sh.Weight;
            e.PrintMode = string.IsNullOrEmpty(sh.PrintMode) ? "Sheet" : sh.PrintMode;
            e.PaperName = sh.PaperName;
            e.Rotation = sh.Rotation;
            e.HAlign = string.IsNullOrEmpty(sh.HAlign) ? "Center" : sh.HAlign;
            e.VAlign = string.IsNullOrEmpty(sh.VAlign) ? "Middle" : sh.VAlign;
            return e;
        }
        e.FontPt = p.FontPt <= 0 ? 11 : p.FontPt;
        e.FontFamily = string.IsNullOrEmpty(p.FontFamily) ? "宋体" : p.FontFamily;
        e.LineSpacing = p.LineSpacing;
        e.CharSpacing = p.CharSpacing;
        e.Weight = p.Weight;
        e.PrintMode = string.IsNullOrEmpty(p.PrintMode) ? "Sheet" : p.PrintMode;
        e.PaperName = p.PaperName;
        e.Rotation = p.Rotation;
        e.HAlign = string.IsNullOrEmpty(p.HAlign) ? "Center" : p.HAlign;
        e.VAlign = string.IsNullOrEmpty(p.VAlign) ? "Middle" : p.VAlign;
        return e;
    }

    /// <summary>打印机显示名：有别名用别名，否则用真实打印机名</summary>
    private string DisplayPrinter(string printer)
    {
        if (string.IsNullOrEmpty(printer)) return "（未绑定）";
        if (_state.PrinterAliases != null && _state.PrinterAliases.TryGetValue(printer, out var a) && !string.IsNullOrWhiteSpace(a))
            return a;
        return printer;
    }

    /// <summary>某台打印机的打印偏移(mm)；未绑定或无记录返回 (0,0)</summary>
    private PrintOffset GetOffset(string? printer)
    {
        if (string.IsNullOrEmpty(printer)) return new PrintOffset();
        if (_state.PrinterOffsets != null && _state.PrinterOffsets.TryGetValue(printer, out var o)) return o;
        return new PrintOffset();
    }

    private void UpdateInfo()
    {
        var p = CurrentPreset;
        _state.CurrentPresetName = PresetKeyAt(cmbPreset.SelectedIndex);
        var e = ApplyEffective(p);
        string printer = string.IsNullOrEmpty(p.PrinterName) ? "（未绑定）" : DisplayPrinter(p.PrinterName);
        string mode = e.PrintMode == "Single" ? "单标签" : "整版";
        string paper = string.IsNullOrEmpty(e.PaperName) ? "驱动默认" : e.PaperName;
        string custom = p.UseCustomParams ? " [自定义参数]" : "";
        lblInfo.Text = $"{p.WidthMm:0.#}×{p.HeightMm:0.#}mm | {e.FontPt}pt {e.FontFamily} | 打印机:{printer} | 纸张:{paper} | {mode} | 对齐:{e.HAlign}/{e.VAlign}{custom}";
    }

    private void OpenSettings()
    {
        using var dlg = new SettingsForm(_state);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            Storage.Save(_state);
            ReloadPresets();
            _currentTypeKey = PresetKeyAt(cmbPreset.SelectedIndex);
            UpdateInfo();
            LoadGridFromType();
            UpdateSeqAndStatus();
            RefreshPreview(true);
        }
    }

    // ============ 表格数据 ============
    private List<string> ReadGridLabels()
    {
        var list = new List<string>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            var parts = new List<string>();
            for (int c = ColDataStart; c < grid.Columns.Count; c++)
            {
                string v = (row.Cells[c].Value?.ToString() ?? "").Trim();
                if (v.Length > 0) parts.Add(v);
            }
            if (parts.Count > 0) list.Add(string.Join("\n", parts));
        }
        return list;
    }

    private void SetRows(List<string[]> rows)
    {
        bool add = grid.AllowUserToAddRows;
        grid.AllowUserToAddRows = false;
        grid.Rows.Clear();
        foreach (var arr in rows) grid.Rows.Add("", arr[0], arr[1], arr[2]);
        grid.AllowUserToAddRows = add;
    }

    private void LoadLabelsToGrid(List<string> labels)
    {
        var rows = (labels ?? new List<string>()).Select(t => t.Split('\n')).Select(lines =>
        {
            var arr = new string[3];
            for (int k = 0; k < 3; k++) arr[k] = k < lines.Length ? lines[k] : "";
            return arr;
        }).ToList();
        SetRows(rows);
        _undo.Clear(); _redo.Clear(); UpdateUndoButtons();
    }

    private void UpdateSeqAndStatus()
    {
        int seq = 0, valid = 0;
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            seq++;
            row.Cells[ColSeq].Value = seq;
            bool has = false;
            for (int c = ColDataStart; c < grid.Columns.Count; c++)
                if ((row.Cells[c].Value?.ToString() ?? "").Trim().Length > 0) { has = true; break; }
            if (has) valid++;
        }
        lblStatus.Text = $"共 {seq} 行，有效标签 {valid} 个（表格自动扩展，直接往下输入即可）";
    }

    // ============ 撤销/重做 ============
    private List<string[]> Snapshot()
    {
        var list = new List<string[]>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow) continue;
            var arr = new string[3];
            for (int c = 0; c < 3; c++) arr[c] = row.Cells[ColDataStart + c].Value?.ToString() ?? "";
            list.Add(arr);
        }
        return list;
    }

    private void ApplySnapshot(List<string[]> snap)
    {
        SetRows(snap);
        UpdateSeqAndStatus();
        RefreshPreview();
    }

    private static bool SameSnapshot(List<string[]> a, List<string[]> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            for (int j = 0; j < 3; j++)
                if (a[i][j] != b[i][j]) return false;
        return true;
    }

    private void PushUndo()
    {
        _undo.Push(Snapshot());
        if (_undo.Count > 100)
        {
            var arr = _undo.ToArray();
            _undo.Clear();
            for (int i = 99; i >= 0; i--) _undo.Push(arr[i]);
        }
        _redo.Clear();
        UpdateUndoButtons();
    }

    private void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(Snapshot());
        ApplySnapshot(_undo.Pop());
        UpdateUndoButtons();
    }

    private void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(Snapshot());
        ApplySnapshot(_redo.Pop());
        UpdateUndoButtons();
    }

    private void UpdateUndoButtons()
    {
        btnUndo.Enabled = _undo.Count > 0;
        btnRedo.Enabled = _redo.Count > 0;
    }

    private void OnCellEndEdit(DataGridViewCellEventArgs e)
    {
        if (_editBefore != null && !SameSnapshot(_editBefore, Snapshot()))
        {
            _undo.Push(_editBefore);
            _redo.Clear();
            UpdateUndoButtons();
        }
        _editBefore = null;
        UpdateSeqAndStatus();
        RememberWord(e);
        RefreshPreview();
    }

    private void RememberWord(DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < ColDataStart) return;
        string v = (grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "").Trim();
        if (v.Length == 0) return;
        if (!_state.Words.Contains(v))
        {
            _state.Words.Add(v);
            Storage.Save(_state);
        }
    }

    // ============ 键盘与编辑控件 ============
    private void Grid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V) { if (!grid.IsCurrentCellInEditMode) PasteClipboard(); e.Handled = true; }
        else if (e.Control && e.KeyCode == Keys.C) { CopySelection(); e.Handled = true; }
        else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
        else if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.Handled = true; }
        else if (e.KeyCode == Keys.Delete) { ClearSelectedRows(); e.Handled = true; }
    }

    private void Grid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (e.Control is TextBox tb)
        {
            tb.KeyDown -= Editor_KeyDown;
            tb.KeyDown += Editor_KeyDown;
        }
    }

    private void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V)
        {
            string text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
            if (text.Contains('\t') || text.Contains('\n') || text.Contains('\r'))
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                grid.EndEdit();
                BeginInvoke(new Action(PasteClipboard));
            }
        }
    }

    // ============ 复制粘贴（区域） ============
    private void CopySelection()
    {
        if (grid.SelectedCells.Count == 0) return;
        int minR = int.MaxValue, maxR = -1, minC = int.MaxValue, maxC = -1;
        foreach (DataGridViewCell c in grid.SelectedCells)
        {
            if (c.ColumnIndex < ColDataStart) continue;
            minR = Math.Min(minR, c.RowIndex); maxR = Math.Max(maxR, c.RowIndex);
            minC = Math.Min(minC, c.ColumnIndex); maxC = Math.Max(maxC, c.ColumnIndex);
        }
        if (maxR < 0) return;
        var sb = new System.Text.StringBuilder();
        for (int r = minR; r <= maxR; r++)
        {
            var cols = new List<string>();
            for (int c = minC; c <= maxC; c++) cols.Add(grid.Rows[r].Cells[c].Value?.ToString() ?? "");
            sb.AppendLine(string.Join("\t", cols));
        }
        try { Clipboard.SetText(sb.ToString().TrimEnd('\r', '\n')); } catch { }
    }

    private void PasteClipboard()
    {
        string text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
        if (text.Trim().Length == 0) return;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        if (lines.Count == 0) return;

        int startRow = grid.CurrentCell?.RowIndex ?? 0;
        int startCol = grid.CurrentCell?.ColumnIndex ?? ColDataStart;
        if (startCol < ColDataStart) startCol = ColDataStart;

        PushUndo();
        int r = startRow;
        foreach (var line in lines)
        {
            var cols = line.Split('\t');
            while (r >= grid.Rows.Count - (grid.AllowUserToAddRows ? 1 : 0))
                grid.Rows.Add("", "", "", "");
            for (int c = 0; c < cols.Length; c++)
            {
                int col = startCol + c;
                if (col >= grid.Columns.Count) break;
                grid.Rows[r].Cells[col].Value = cols[c];
            }
            r++;
        }
        UpdateSeqAndStatus();
        RefreshPreview();
    }

    /// <summary>按固定行数把纯文本行归入每个标签（每 linesPerRow 行 = 一个标签）</summary>
    private void PasteBlock(int linesPerRow)
    {
        string text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
        if (text.Trim().Length == 0) return;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        if (lines.Count == 0) return;

        int startRow = grid.CurrentCell?.RowIndex ?? 0;
        if (startRow < 0) startRow = 0;

        PushUndo();
        int r = startRow;
        for (int i = 0; i < lines.Count; i += linesPerRow)
        {
            while (r >= grid.Rows.Count - (grid.AllowUserToAddRows ? 1 : 0))
                grid.Rows.Add("", "", "", "");
            for (int k = 0; k < linesPerRow; k++)
            {
                int col = ColDataStart + k;
                if (col >= grid.Columns.Count) break;
                grid.Rows[r].Cells[col].Value = i + k < lines.Count ? lines[i + k].Trim() : "";
            }
            r++;
        }
        UpdateSeqAndStatus();
        RefreshPreview();
    }

    /// <summary>把纯文本按每 combineLines 行合并成一行（用连接符连接），每行=一个标签</summary>
    private void PasteCombine(int combineLines)
    {
        string text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
        if (text.Trim().Length == 0) return;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        if (lines.Count == 0) return;

        string sep = _state.MergeSeparator ?? " ";

        int startRow = grid.CurrentCell?.RowIndex ?? 0;
        if (startRow < 0) startRow = 0;

        PushUndo();
        int r = startRow;
        for (int i = 0; i < lines.Count; i += combineLines)
        {
            var parts = new List<string>();
            for (int k = 0; k < combineLines && i + k < lines.Count; k++) parts.Add(lines[i + k].Trim());
            while (r >= grid.Rows.Count - (grid.AllowUserToAddRows ? 1 : 0))
                grid.Rows.Add("", "", "", "");
            grid.Rows[r].Cells[ColDataStart].Value = string.Join(sep, parts);
            r++;
        }
        UpdateSeqAndStatus();
        RefreshPreview();
    }

    private void ClearSelectedRows()
    {
        if (grid.SelectedCells.Count == 0) return;
        PushUndo();
        foreach (DataGridViewCell cell in grid.SelectedCells)
            if (cell.RowIndex >= 0 && cell.ColumnIndex >= ColDataStart && !grid.Rows[cell.RowIndex].IsNewRow)
                grid.Rows[cell.RowIndex].Cells[cell.ColumnIndex].Value = "";
        UpdateSeqAndStatus();
        RefreshPreview();
    }

    private void DeleteSelectedRows()
    {
        var rows = grid.SelectedCells.Cast<DataGridViewCell>().Select(c => c.RowIndex).Distinct().OrderByDescending(i => i).ToList();
        if (rows.Count == 0) return;
        PushUndo();
        foreach (int i in rows) if (i >= 0 && i < grid.Rows.Count && !grid.Rows[i].IsNewRow) grid.Rows.RemoveAt(i);
        UpdateSeqAndStatus();
        RefreshPreview();
    }

    private void BuildGridMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("复制", null, (_, _) => CopySelection());
        menu.Items.Add("粘贴", null, (_, _) => PasteClipboard());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("分行粘贴（2行/标签）", null, (_, _) => PasteBlock(2));
        menu.Items.Add("分行粘贴（3行/标签）", null, (_, _) => PasteBlock(3));
        menu.Items.Add("合并粘贴（2行/标签）", null, (_, _) => PasteCombine(2));
        menu.Items.Add("合并粘贴（3行/标签）", null, (_, _) => PasteCombine(3));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("清空选中", null, (_, _) => ClearSelectedRows());
        menu.Items.Add("删除选中行", null, (_, _) => DeleteSelectedRows());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("递增填充（同一列）", null, (_, _) => FillIncrement());
        menu.Items.Add("填充至…（同一列）", null, (_, _) => FillTo());
        grid.ContextMenuStrip = menu;
        grid.CellMouseDown += Grid_CellMouseDown;
    }

    /// <summary>右键时先把鼠标所在单元格选中（若已在选区外则移动选区），再弹菜单</summary>
    private void Grid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0) return;
        var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        if (!cell.Selected)
        {
            grid.ClearSelection();
            grid.CurrentCell = cell;
        }
        grid.Focus();
    }

    private void FillIncrement()
    {
        int col = grid.CurrentCell?.ColumnIndex ?? -1;
        if (col < ColDataStart) return;
        var rows = grid.SelectedCells.Cast<DataGridViewCell>().Where(c => c.ColumnIndex == col).Select(c => c.RowIndex).Distinct().OrderBy(i => i).ToList();
        if (rows.Count < 2) { MessageBox.Show(this, "请选中同一列至少两行", "提示"); return; }
        PushUndo();
        string cur = grid.Rows[rows[0]].Cells[col].Value?.ToString() ?? "";
        for (int k = 1; k < rows.Count; k++)
        {
            cur = IncrementLastNumber(cur);
            grid.Rows[rows[k]].Cells[col].Value = cur;
        }
        UpdateSeqAndStatus();
        RefreshPreview();
    }

    private void FillTo()
    {
        int col = grid.CurrentCell?.ColumnIndex ?? -1;
        if (col < ColDataStart) return;
        var rows = grid.SelectedCells.Cast<DataGridViewCell>().Where(c => c.ColumnIndex == col).Select(c => c.RowIndex).Distinct().OrderBy(i => i).ToList();
        if (rows.Count < 1) return;
        int startRow = rows[0];
        string cur = grid.Rows[startRow].Cells[col].Value?.ToString() ?? "";
        var last = GetLastNumber(cur);
        if (last == null) { MessageBox.Show(this, "起始单元格不含数字", "提示"); return; }
        string? input = Prompt($"当前数字 {last}，请输入目标数字：", "填充至");
        if (!int.TryParse(input, out int target) || target <= last) return;
        PushUndo();
        int count = target - last.Value;
        for (int k = 1; k <= count; k++)
        {
            int rr = startRow + k;
            while (rr >= grid.Rows.Count - (grid.AllowUserToAddRows ? 1 : 0)) grid.Rows.Add("", "", "", "");
            cur = IncrementLastNumber(cur);
            grid.Rows[rr].Cells[col].Value = cur;
        }
        UpdateSeqAndStatus();
        RefreshPreview();
    }

    private static string IncrementLastNumber(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)(?!.*\d)");
        if (!m.Success) return text;
        string numStr = m.Groups[1].Value;
        long num = long.Parse(numStr) + 1;
        string newStr = (numStr.Length > 1 && numStr[0] == '0') ? num.ToString().PadLeft(numStr.Length, '0') : num.ToString();
        return text.Substring(0, m.Groups[1].Index) + newStr + text.Substring(m.Groups[1].Index + numStr.Length);
    }

    private static int? GetLastNumber(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)(?!.*\d)");
        return m.Success ? int.Parse(m.Groups[1].Value) : (int?)null;
    }

    private static string? Prompt(string message, string title)
    {
        using var dlg = new Form
        {
            Text = title, ClientSize = new Size(320, 130), Font = new Font("微软雅黑", 9F),
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };
        dlg.Controls.Add(new Label { Text = message, Left = 14, Top = 16, AutoSize = true });
        var tb = new TextBox { Left = 14, Top = 46, Width = 292 };
        dlg.Controls.Add(tb);
        var ok = new Button { Text = "确定", Left = 150, Top = 84, Width = 76, Height = 28, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "取消", Left = 230, Top = 84, Width = 76, Height = 28, DialogResult = DialogResult.Cancel };
        dlg.AcceptButton = ok; dlg.CancelButton = cancel;
        dlg.Controls.AddRange(new Control[] { ok, cancel });
        return dlg.ShowDialog() == DialogResult.OK ? tb.Text : null;
    }

    // ============ 打印数据（单元 / 规格 / 分组） ============
    private List<PrintUnit> BuildUnits()
    {
        SaveCurrentTypeContent();
        var units = new List<PrintUnit>();
        foreach (var kv in _types)
        {
            if (kv.Value.Labels.Count == 0) continue;
            units.Add(new PrintUnit
            {
                Preset = FindPreset(kv.Key),
                TypeKey = kv.Key,
                Labels = kv.Value.Labels,
                FontFamily = kv.Value.FontFamily,
                FontSize = kv.Value.FontSize,
                AutoFit = kv.Value.AutoFit,
                AutoSize = kv.Value.AutoSize,
                LineSpacing = kv.Value.LineSpacing,
                CharSpacing = kv.Value.CharSpacing,
                Weight = kv.Value.Weight
            });
        }
        return units;
    }

    private (List<LabelSpec> specs, List<SpecMeta> metas) BuildSpecs()
    {
        var specs = new List<LabelSpec>();
        var metas = new List<SpecMeta>();
        int idx = 0;
        foreach (var u in BuildUnits())
        {
            bool cur = u.TypeKey == _currentTypeKey;
            for (int li = 0; li < u.Labels.Count; li++)
            {
                var spec = SpecFromPreset(u.Preset, u.Labels[li], u.FontFamily, u.FontSize, u.AutoFit, u.AutoSize,
                    u.LineSpacing, u.CharSpacing, u.Weight);
                spec.Index = idx;
                var eff = ApplyEffective(u.Preset);
                var off = GetOffset(u.Preset.PrinterName);
                spec.OffsetX = off.X;
                spec.OffsetY = off.Y;
                specs.Add(spec);
                metas.Add(new SpecMeta
                {
                    Index = idx,
                    Printer = u.Preset.PrinterName ?? "",
                    Single = eff.PrintMode == "Single",
                    Preset = u.Preset,
                    TypeKey = u.TypeKey,
                    LabelIndex = li,
                    IsCurrentType = cur,
                    FontFamily = u.FontFamily,
                    FontSize = u.FontSize,
                    AutoFit = u.AutoFit,
                    AutoSize = u.AutoSize,
                    LineSpacing = u.LineSpacing,
                    CharSpacing = u.CharSpacing,
                    Weight = u.Weight
                });
                idx++;
            }
        }
        return (specs, metas);
    }

    private LabelSpec SpecFromPreset(Preset p, string text, string? fontFamily, double fontSize, bool autoFit, bool autoSize,
        double lineSpacing, double charSpacing, double weight)
    {
        var eff = ApplyEffective(p);
        return new LabelSpec
        {
            W = p.WidthMm, H = p.HeightMm,
            FontPt = fontSize > 0 ? fontSize : eff.FontPt,
            FontFamily = string.IsNullOrEmpty(fontFamily) ? eff.FontFamily : fontFamily,
            Text = text, Rotation = eff.Rotation, HAlign = eff.HAlign, VAlign = eff.VAlign,
            AutoFit = autoFit, AutoSize = autoSize,
            LineSpacing = Math.Abs(lineSpacing) > 0.001 ? lineSpacing : eff.LineSpacing,
            CharSpacing = Math.Abs(charSpacing) > 0.001 ? charSpacing : eff.CharSpacing,
            Weight = weight > 0.001 ? weight : eff.Weight
        };
    }

    // ============ 预览 ============
    private void ShowPreview()
    {
        if (_preview == null || _preview.IsDisposed)
        {
            _preview = new PreviewForm();
            _preview.Initialize(LocateLabel, () => RefreshPreview(true), ApplyPreviewFont, SavePreviewFont);
            _preview.FormClosed += (_, _) => _preview = null;
        }
        _preview.Show(this);
        _preview.BringToFront();
        RefreshPreview(true);
    }

    private void RefreshPreview(bool force = false)
    {
        if (_preview == null || _preview.IsDisposed) return;
        if (!force && !chkRealtime.Checked) return;
        _lastSources = BuildSources();
        _preview.SetSources(_lastSources);
    }

    private List<PreviewSource> BuildSources()
    {
        var (specs, metas) = BuildSpecs();
        var sources = new List<PreviewSource>();

        // 每台打印机汇总一条
        foreach (var grp in metas.GroupBy(m => m.Printer).OrderBy(g => g.Key))
        {
            var list = grp.ToList();
            string printer = grp.Key;
            string? paperName = list.Select(m => m.Preset.PaperName).FirstOrDefault(x => !string.IsNullOrEmpty(x));
            var (pw, ph) = ResolvePaper(printer, paperName);
            var rep = list.FirstOrDefault(m => m.IsCurrentType) ?? list[0];
            var src = new PreviewSource
            {
                Name = "打印机：" + DisplayPrinter(printer) + $"（纸{paperName ?? "默认"} {pw:0.#}×{ph:0.#}mm）",
                FontFamily = rep.FontFamily,
                FontSize = rep.FontSize,
                AutoFit = rep.AutoFit,
                AutoSize = rep.AutoSize,
                LineSpacing = rep.LineSpacing,
                CharSpacing = rep.CharSpacing,
                Weight = rep.Weight,
                HasCurrentType = list.Any(m => m.IsCurrentType),
                TypeKeys = list.Select(m => m.TypeKey).Distinct().ToList()
            };
            src.Pages = BuildPagesFor(list, specs, pw, ph);
            var sheetSpecs = list.Where(m => !m.Single).Select(m => specs[m.Index]).ToList();
            var singleSpecs = list.Where(m => m.Single).Select(m => specs[m.Index]).ToList();
            src.PrintAction = () => PrintGroup(printer, paperName, pw, ph, sheetSpecs, singleSpecs);
            sources.Add(src);
        }

        // 每个标签类型各一条
        foreach (var grp in metas.GroupBy(m => m.TypeKey))
        {
            var list = grp.ToList();
            string key = grp.Key;
            var p = FindPreset(key);
            var (pw, ph) = ResolvePaper(p.PrinterName ?? "", p.PaperName);
            var rep = list.FirstOrDefault(m => m.IsCurrentType) ?? list[0];
            var src = new PreviewSource
            {
                Name = $"标签类型：{key}（{list.Count}个）",
                FontFamily = rep.FontFamily,
                FontSize = rep.FontSize,
                AutoFit = rep.AutoFit,
                AutoSize = rep.AutoSize,
                LineSpacing = rep.LineSpacing,
                CharSpacing = rep.CharSpacing,
                Weight = rep.Weight,
                HasCurrentType = list.Any(m => m.IsCurrentType),
                TypeKeys = new List<string> { key }
            };
            src.Pages = BuildPagesFor(list, specs, pw, ph);
            var sheetSpecs = list.Where(m => !m.Single).Select(m => specs[m.Index]).ToList();
            var singleSpecs = list.Where(m => m.Single).Select(m => specs[m.Index]).ToList();
            src.PrintAction = () => PrintGroup(p.PrinterName ?? "", p.PaperName, pw, ph, sheetSpecs, singleSpecs);
            sources.Add(src);
        }
        return sources;
    }

    private void ApplyPreviewFont(int sourceIndex, FontOverride o)
    {
        if (sourceIndex < 0 || sourceIndex >= _lastSources.Count) return;
        var s = _lastSources[sourceIndex];
        foreach (var key in s.TypeKeys)
        {
            if (!_types.TryGetValue(key, out var td)) { td = new TypeData(); _types[key] = td; }
            td.FontFamily = o.Family; td.FontSize = o.Size; td.AutoFit = o.AutoFit; td.AutoSize = o.AutoSize;
            td.LineSpacing = o.LineSpacing; td.CharSpacing = o.CharSpacing; td.Weight = o.Weight;
        }
        RefreshPreview(true);
    }

    private void SavePreviewFont()
    {
        SaveCurrentCabinet();
        Storage.Save(_state);
        MessageBox.Show(this, "字体设置已保存到当前柜子");
    }

    private (float w, float h) ResolvePaper(string printer, string? paperName)
    {
        var sz = PrintService.GetPaperSizeMm(printer, paperName);
        if (sz != null) return (sz.Value.w, sz.Value.h);
        return ((float)_state.Paper.WidthMm, (float)_state.Paper.HeightMm);
    }

    private static List<PreviewPageInfo> BuildPagesFor(List<SpecMeta> grp, List<LabelSpec> specs, float pw, float ph)
    {
        var pages = new List<PreviewPageInfo>();
        var sheetSpecs = grp.Where(m => !m.Single).Select(m => specs[m.Index]).ToList();
        if (sheetSpecs.Count > 0)
            foreach (var pg in PrintService.LayoutSheet(sheetSpecs, pw, ph))
                pages.Add(new PreviewPageInfo { Printer = grp[0].Printer, Labels = pg, PaperW = pw, PaperH = ph, Single = false });
        foreach (var m in grp.Where(m => m.Single))
        {
            var spec = specs[m.Index];
            foreach (var pg in PrintService.LayoutSingle(new[] { spec }))
                pages.Add(new PreviewPageInfo { Printer = m.Printer, Labels = pg, PaperW = (float)spec.W, PaperH = (float)spec.H, Single = true });
        }
        return pages;
    }

    private void PrintGroup(string printer, string? paperName, float pw, float ph, List<LabelSpec> sheet, List<LabelSpec> single)
    {
        if (string.IsNullOrEmpty(printer)) { MessageBox.Show(this, "该内容未绑定打印机，无法打印", "提示"); return; }
        try
        {
            if (sheet.Count > 0)
                PrintService.Print(new PrintRequest
                {
                    PrinterName = printer, PaperName = paperName,
                    PaperWmm = pw, PaperHmm = ph,
                    SingleMode = false, Labels = sheet
                });
            if (single.Count > 0)
                PrintService.Print(new PrintRequest
                {
                    PrinterName = printer, PaperName = paperName,
                    PaperWmm = pw, PaperHmm = ph,
                    SingleMode = true, Labels = single
                });
            MessageBox.Show(this, $"已发送到打印机：{printer}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "打印失败：" + ex.Message);
        }
    }

    private void LocateLabel(int index)
    {
        var (_, metas) = BuildSpecs();
        if (index < 0 || index >= metas.Count) return;
        var m = metas[index];
        Activate();
        if (m.TypeKey != _currentTypeKey)
        {
            int idx = PresetIndexOf(m.TypeKey);
            if (idx >= 0) cmbPreset.SelectedIndex = idx; // 触发 OnPresetChanged：存旧类型、载新类型
        }
        HighlightLabelRow(m.LabelIndex);
    }

    private void HighlightLabelRow(int labelIndex)
    {
        if (labelIndex < 0) return;
        int count = -1;
        for (int r = 0; r < grid.Rows.Count; r++)
        {
            if (grid.Rows[r].IsNewRow) continue;
            bool has = false;
            for (int c = ColDataStart; c < grid.Columns.Count; c++)
                if ((grid.Rows[r].Cells[c].Value?.ToString() ?? "").Trim().Length > 0) { has = true; break; }
            if (!has) continue;
            count++;
            if (count == labelIndex)
            {
                grid.CurrentCell = grid.Rows[r].Cells[ColDataStart];
                grid.BeginEdit(true);
                grid.FirstDisplayedScrollingRowIndex = r;
                return;
            }
        }
    }

    // ============ 打印设置 ============
    private void ShowPrintSettings()
    {
        string printer = CurrentPreset.PrinterName ?? "";
        if (printer.Length == 0) { MessageBox.Show(this, "请先在“设置”里为该标签类型绑定打印机", "提示"); return; }
        try
        {
            using var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printer;
            using var dlg = new PrintDialog { Document = doc, UseEXDialog = true, AllowSomePages = false };
            dlg.ShowDialog(this);
        }
        catch (Exception ex) { MessageBox.Show(this, "打开打印设置失败：" + ex.Message); }
    }
}
