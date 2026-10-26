using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;

namespace LabelTool;

/// <summary>一个待打印的标签</summary>
public class LabelSpec
{
    public int Index { get; set; } = -1;
    public double W { get; set; }
    public double H { get; set; }
    public double FontPt { get; set; } = 11;
    public string FontFamily { get; set; } = "宋体";
    public string Text { get; set; } = "";
    public double Rotation { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    /// <summary>水平对齐：Left/Center/Right</summary>
    public string HAlign { get; set; } = "Center";
    /// <summary>垂直对齐：Top/Middle/Bottom</summary>
    public string VAlign { get; set; } = "Middle";
    public bool AutoFit { get; set; }
    public bool AutoSize { get; set; }
    public double LineSpacing { get; set; }
    public double CharSpacing { get; set; }
    public double Weight { get; set; }
}

/// <summary>已定位到页面上的标签（单位 mm）</summary>
public class PlacedLabel
{
    public int Index { get; set; } = -1;
    public float X { get; set; }
    public float Y { get; set; }
    public float W { get; set; }
    public float H { get; set; }
    public string Text { get; set; } = "";
    public double FontPt { get; set; } = 11;
    public string FontFamily { get; set; } = "宋体";
    /// <summary>标签整体旋转角度（0/90/180/270），内容与边框一起旋转</summary>
    public double Rotation { get; set; }
    /// <summary>水平对齐：Left/Center/Right</summary>
    public string HAlign { get; set; } = "Center";
    /// <summary>垂直对齐：Top/Middle/Bottom</summary>
    public string VAlign { get; set; } = "Middle";
    public bool AutoFit { get; set; }
    public bool AutoSize { get; set; }
    public double LineSpacing { get; set; }
    public double CharSpacing { get; set; }
    public double Weight { get; set; }
}

public class PrintRequest
{
    public string PrinterName { get; set; } = "";
    public string? PaperName { get; set; }
    public float PaperWmm { get; set; } = 210;
    public float PaperHmm { get; set; } = 297;
    public bool SingleMode { get; set; }
    public List<LabelSpec> Labels { get; set; } = new();
}

public static class PrintService
{
    public static List<string> GetPrinters()
    {
        var list = new List<string>();
        foreach (string p in PrinterSettings.InstalledPrinters) list.Add(p);
        return list;
    }

    public static string GetDefaultPrinter()
    {
        try { return new PrinterSettings().PrinterName; } catch { return ""; }
    }

    public static List<PaperSize> GetPapers(string printerName)
    {
        var result = new List<PaperSize>();
        try
        {
            using var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printerName;
            foreach (PaperSize ps in doc.PrinterSettings.PaperSizes) result.Add(ps);
        }
        catch { }
        return result;
    }

    public static (float dpiX, float dpiY) GetDpi(string printerName)
    {
        try
        {
            using var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printerName;
            using var mg = doc.PrinterSettings.CreateMeasurementGraphics();
            return (mg.DpiX, mg.DpiY);
        }
        catch { return (203f, 203f); }
    }

    /// <summary>取驱动纸张尺寸（mm）。找不到返回 null。</summary>
    public static (float w, float h)? GetPaperSizeMm(string printerName, string? paperName)
    {
        if (string.IsNullOrEmpty(printerName) || string.IsNullOrEmpty(paperName)) return null;
        foreach (var ps in GetPapers(printerName))
            if (ps.PaperName == paperName)
            {
                float w = ps.Width / 100f * 25.4f;
                float h = ps.Height / 100f * 25.4f;
                if (w < 3f || h < 3f || w > 5000f || h > 5000f) return null;
                return (w, h);
            }
        return null;
    }

    // ---------- 排版 ----------
    public static List<List<PlacedLabel>> LayoutSheet(IReadOnlyList<LabelSpec> labels, float paperW, float paperH)
    {
        var order = new List<(double w, double h)>();
        var map = new Dictionary<(double w, double h), List<LabelSpec>>();
        foreach (var lb in labels)
        {
            var key = (Math.Round(lb.W, 2), Math.Round(lb.H, 2));
            if (!map.TryGetValue(key, out var list)) { list = new(); map[key] = list; order.Add(key); }
            list.Add(lb);
        }

        var pages = new List<List<PlacedLabel>>();
        var current = new List<PlacedLabel>();
        float y = 0;

        foreach (var key in order)
        {
            var items = map[key];
            var rows = new List<(List<LabelSpec> items, double h)>();
            var cur = new List<LabelSpec>();
            double curW = 0, maxH = 0;
            foreach (var it in items)
            {
                if (cur.Count > 0 && curW + it.W > paperW)
                {
                    rows.Add((cur, maxH));
                    cur = new List<LabelSpec> { it };
                    curW = it.W; maxH = it.H;
                }
                else
                {
                    cur.Add(it); curW += it.W; if (it.H > maxH) maxH = it.H;
                }
            }
            if (cur.Count > 0) rows.Add((cur, maxH));

            foreach (var (rowItems, rowH) in rows)
            {
                if (y + rowH > paperH && current.Count > 0)
                {
                    pages.Add(current);
                    current = new List<PlacedLabel>();
                    y = 0;
                }
                float x = 0;
                foreach (var it in rowItems)
                {
                    current.Add(new PlacedLabel
                    {
                        Index = it.Index,
                        X = x + (float)it.OffsetX, Y = y + (float)it.OffsetY, W = (float)it.W, H = (float)it.H,
                        Text = it.Text, FontPt = it.FontPt, FontFamily = it.FontFamily,
                        Rotation = it.Rotation, HAlign = it.HAlign, VAlign = it.VAlign,
                        AutoFit = it.AutoFit, AutoSize = it.AutoSize,
                        LineSpacing = it.LineSpacing, CharSpacing = it.CharSpacing, Weight = it.Weight
                    });
                    x += (float)it.W;
                }
                y += (float)rowH;
            }
        }
        if (current.Count > 0) pages.Add(current);
        return pages;
    }

    public static List<List<PlacedLabel>> LayoutSingle(IReadOnlyList<LabelSpec> labels)
    {
        var pages = new List<List<PlacedLabel>>();
        foreach (var lb in labels)
        {
            pages.Add(new List<PlacedLabel>
            {
                new PlacedLabel
                {
                    Index = lb.Index,
                    X = (float)lb.OffsetX, Y = (float)lb.OffsetY, W = (float)lb.W, H = (float)lb.H,
                    Text = lb.Text, FontPt = lb.FontPt, FontFamily = lb.FontFamily,
                    Rotation = lb.Rotation, HAlign = lb.HAlign, VAlign = lb.VAlign,
                    AutoFit = lb.AutoFit, AutoSize = lb.AutoSize,
                    LineSpacing = lb.LineSpacing, CharSpacing = lb.CharSpacing, Weight = lb.Weight
                }
            });
        }
        return pages;
    }

    // ---------- 渲染 ----------
    public static Bitmap RenderPage(IReadOnlyList<PlacedLabel> placed, float paperWmm, float paperHmm, float dpiX, float dpiY)
    {
        int pw = Math.Max(1, (int)Math.Round(paperWmm / 25.4f * dpiX));
        int ph = Math.Max(1, (int)Math.Round(paperHmm / 25.4f * dpiY));
        var bmp = new Bitmap(pw, ph);
        bmp.SetResolution(dpiX, dpiY);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.None;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        float pxmm = dpiX / 25.4f;

        foreach (var p in placed)
        {
            float x = p.X * pxmm, y = p.Y * pxmm, w = p.W * pxmm, h = p.H * pxmm;
            int rot = ((int)Math.Round(p.Rotation) % 360 + 360) % 360;
            if (rot != 0 && rot != 90 && rot != 180 && rot != 270) rot = 0;
            // 内容虚拟框：90/270 时交换宽高，自动缩小按旋转后的实际可见范围适配
            float cw = (rot == 90 || rot == 270) ? h : w;
            float ch = (rot == 90 || rot == 270) ? w : h;

            // 边框线：用 1 像素宽的笔并把线画在像素中心（+0.5），
            // 避免 x=0/y=0 或右/下边界处被裁掉一半导致某些打印机（如惠普）丢失左/顶边线
            float penPx = Math.Max(1f, (float)Math.Round(dpiX / 400f));
            using (var pen = new Pen(Color.Black, penPx))
            {
                float inset = penPx / 2f;
                float x0 = x + inset, y0 = y + inset;
                float x1 = x + w - inset, y1 = y + h - inset;
                if (x1 > x0 && y1 > y0)
                {
                    g.DrawLine(pen, x0, y0, x1, y0);
                    g.DrawLine(pen, x0, y0, x0, y1);
                    g.DrawLine(pen, x1, y0, x1, y1);
                    g.DrawLine(pen, x0, y1, x1, y1);
                }
            }
            float lineSpacingPx = (float)(p.LineSpacing * pxmm);
            float charSpacingPx = (float)(p.CharSpacing * pxmm);
            float weightPx = (float)(p.Weight * pxmm);
            using var font = MakeFont(g, p.Text, p.FontFamily, (float)p.FontPt, cw, ch, p.AutoFit, p.AutoSize, lineSpacingPx, charSpacingPx, weightPx);
            var state = g.Save();
            g.TranslateTransform(x + w / 2f, y + h / 2f);
            if (rot != 0) g.RotateTransform(rot);
            g.TranslateTransform(-cw / 2f, -ch / 2f);
            DrawBlock(g, p.Text, font, Brushes.Black, new RectangleF(0, 0, cw, ch), lineSpacingPx, charSpacingPx, weightPx, p.HAlign, p.VAlign);
            g.Restore(state);
        }
        return bmp;
    }

    private static Font MakeFont(Graphics g, string text, string family, float pt, float w, float h,
        bool autoFit, bool autoSize, float lineSpacingPx, float charSpacingPx, float weightPx)
    {
        if (!autoFit && !autoSize)
            return new Font(family, Math.Max(1f, pt), FontStyle.Regular, GraphicsUnit.Point);

        float start = pt;
        if (autoSize)
        {
            // 上界：等于框高的点数，再放大以覆盖（实际会缩到刚好填满）
            float boxPt = h / (g.DpiY / 72f);
            start = Math.Max(pt, boxPt * 1.5f);
        }
        // 自动缩小不能超过预设字号；自适应字号可取最大值
        float cap = autoSize ? start : Math.Max(1f, pt);

        Font Fits(float size)
        {
            var f = new Font(family, size, FontStyle.Regular, GraphicsUnit.Point);
            var sz = MeasureBlock(g, text, f, lineSpacingPx, charSpacingPx, weightPx);
            if (sz.Width <= w + 0.5f && sz.Height <= h + 0.5f) return f;
            f.Dispose();
            return null!;
        }

        try
        {
            start = Math.Max(1f, start);
            float step = Math.Max(0.5f, start / 60f);
            Font? best = null;
            for (float size = start; size > 3f; size -= step)
            {
                var f = Fits(size);
                if (f != null) { best = f; break; }
            }
            if (best == null) return new Font(family, 3f, FontStyle.Regular, GraphicsUnit.Point);

            // 细化：在粗步长结果上以 0.5 递增直到不再适配（不超过 cap）
            while (best.Size + 0.5f <= cap)
            {
                var up = Fits(best.Size + 0.5f);
                if (up == null) break;
                best.Dispose();
                best = up;
            }
            return best;
        }
        catch { }
        return new Font(family, 3f, FontStyle.Regular, GraphicsUnit.Point);
    }

    private static SizeF MeasureBlock(Graphics g, string text, Font font, float lineSpacingPx, float charSpacingPx, float weightPx)
    {
        var lines = text.Split('\n');
        float lineH = Math.Max(1f, font.GetHeight(g) + lineSpacingPx);
        float maxW = 0f;
        foreach (var line in lines)
        {
            float lw = MeasureLineWidth(g, line, font, charSpacingPx) + weightPx;
            if (lw > maxW) maxW = lw;
        }
        return new SizeF(maxW, lineH * lines.Length + weightPx);
    }

    private static float MeasureLineWidth(Graphics g, string line, Font font, float charSpacingPx)
    {
        if (line.Length == 0) return 0f;
        if (Math.Abs(charSpacingPx) < 0.01f)
            return g.MeasureString(line, font, PointF.Empty, StringFormat.GenericTypographic).Width;
        float w = 0f;
        foreach (char c in line)
            w += g.MeasureString(c.ToString(), font, PointF.Empty, StringFormat.GenericTypographic).Width + charSpacingPx;
        return w - charSpacingPx;
    }

    private static void DrawBlock(Graphics g, string text, Font font, Brush brush, RectangleF rect,
        float lineSpacingPx, float charSpacingPx, float weightPx, string hAlign = "Center", string vAlign = "Middle")
    {
        var lines = text.Split('\n');
        float lineH = Math.Max(1f, font.GetHeight(g) + lineSpacingPx);
        float totalH = lineH * lines.Length;
        float startY = vAlign switch
        {
            "Top" => rect.Y,
            "Bottom" => rect.Bottom - totalH,
            _ => rect.Y + (rect.Height - totalH) / 2f
        };
        var sf = StringFormat.GenericTypographic;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            float lw = MeasureLineWidth(g, line, font, charSpacingPx);
            float lx = hAlign switch
            {
                "Left" => rect.X,
                "Right" => rect.Right - lw,
                _ => rect.X + (rect.Width - lw) / 2f
            };
            float ly = startY + i * lineH;
            if (Math.Abs(charSpacingPx) < 0.01f)
            {
                DrawText(g, line, font, brush, lx, ly, sf, weightPx);
            }
            else
            {
                foreach (char c in line)
                {
                    string s = c.ToString();
                    DrawText(g, s, font, brush, lx, ly, sf, weightPx);
                    lx += g.MeasureString(s, font, PointF.Empty, sf).Width + charSpacingPx;
                }
            }
        }
    }

    private static void DrawText(Graphics g, string s, Font font, Brush brush, float x, float y, StringFormat sf, float weightPx)
    {
        if (weightPx <= 0.05f) { g.DrawString(s, font, brush, x, y, sf); return; }
        // 加粗改用多次偏移绘制，避免 GraphicsPath 描边导致颗粒感
        float step = Math.Min(weightPx, 0.6f);
        int count = (int)Math.Ceiling(weightPx / Math.Max(0.12f, step));
        if (count < 1) count = 1;
        if (count > 4) count = 4;
        for (int dx = 0; dx <= count; dx++)
        {
            for (int dy = 0; dy <= count; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                float ox = dx * step / count;
                float oy = dy * step / count;
                g.DrawString(s, font, brush, x + ox, y + oy, sf);
            }
        }
        g.DrawString(s, font, brush, x, y, sf);
    }

    // ---------- 打印 ----------
    public static void Print(PrintRequest req)
    {
        using var doc = new PrintDocument();
        doc.PrinterSettings.PrinterName = req.PrinterName;
        doc.PrintController = new StandardPrintController();

        if (!string.IsNullOrEmpty(req.PaperName))
        {
            foreach (PaperSize ps in doc.PrinterSettings.PaperSizes)
            {
                if (ps.PaperName == req.PaperName) { doc.DefaultPageSettings.PaperSize = ps; break; }
            }
        }

        var (dpiX, dpiY) = GetDpi(req.PrinterName);

        var pages = new List<Bitmap>();
        try
        {
            if (req.SingleMode)
            {
                foreach (var lb in req.Labels)
                {
                    pages.Add(RenderPage(
                        new[] { new PlacedLabel { X = (float)lb.OffsetX, Y = (float)lb.OffsetY, W = (float)lb.W, H = (float)lb.H, Text = lb.Text, FontPt = lb.FontPt, FontFamily = lb.FontFamily, Rotation = lb.Rotation, HAlign = lb.HAlign, VAlign = lb.VAlign, AutoFit = lb.AutoFit, AutoSize = lb.AutoSize, LineSpacing = lb.LineSpacing, CharSpacing = lb.CharSpacing, Weight = lb.Weight } },
                        (float)lb.W, (float)lb.H, dpiX, dpiY));
                }
            }
            else
            {
                foreach (var pg in LayoutSheet(req.Labels, req.PaperWmm, req.PaperHmm))
                    pages.Add(RenderPage(pg, req.PaperWmm, req.PaperHmm, dpiX, dpiY));
            }

            if (pages.Count == 0) return;

            int idx = 0;
            doc.PrintPage += (s, e) =>
            {
                var bmp = pages[idx];
                var g = e.Graphics!;
                g.PageUnit = GraphicsUnit.Millimeter;
                // 1:1 像素映射，避免重采样把 1 像素边框线变成灰色而被打印机丢弃
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.SmoothingMode = SmoothingMode.None;
                float wmm = bmp.Width / dpiX * 25.4f;
                float hmm = bmp.Height / dpiY * 25.4f;
                g.DrawImage(bmp, new RectangleF(0, 0, wmm, hmm));
                idx++;
                e.HasMorePages = idx < pages.Count;
            };

            doc.Print();
        }
        finally
        {
            foreach (var b in pages) b.Dispose();
        }
    }
}
