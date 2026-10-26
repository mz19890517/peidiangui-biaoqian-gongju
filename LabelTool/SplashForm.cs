using System.Reflection;

namespace LabelTool;

/// <summary>启动页：主标题“智能标签打印机”，署名作者及版本信息，2 秒后进入主界面</summary>
public class SplashForm : Form
{
    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 280);
        BackColor = Color.FromArgb(245, 248, 252);
        ShowInTaskbar = true;

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0.0";

        var title = new Label
        {
            Text = "智能标签打印机",
            Font = new Font("微软雅黑", 26F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 90, 160),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 120
        };
        var author = new Label
        {
            Text = "孟国胜",
            Font = new Font("微软雅黑", 12F),
            ForeColor = Color.DimGray,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 40
        };
        var ver = new Label
        {
            Text = $"版本：{version}",
            Font = new Font("微软雅黑", 9F),
            ForeColor = Color.Gray,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 30
        };
        Controls.Add(ver);
        Controls.Add(author);
        Controls.Add(title);

        Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
            Color.FromArgb(150, 180, 215), ButtonBorderStyle.Solid);
    }
}
