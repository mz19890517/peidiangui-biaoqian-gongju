namespace LabelTool;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        // 启动页：显示约 2 秒后进入主界面
        using (var splash = new SplashForm())
        {
            splash.Show();
            splash.Refresh();
            var end = DateTime.Now.AddSeconds(2);
            while (DateTime.Now < end)
            {
                Application.DoEvents();
                Thread.Sleep(30);
            }
            splash.Hide();
        }

        Application.Run(new MainForm());
    }    
}