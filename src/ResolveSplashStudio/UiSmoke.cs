using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

public static class UiSmoke
{
    private static int ticks;
    private static bool scanClicked;
    private static bool previewSeen;
    private static bool failed;
    private static string failure = "";

    private static object GetField(object target, string name)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) throw new Exception("Missing field: " + name);
        return f.GetValue(target);
    }

    [STAThread]
    public static int Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        MainForm form = new MainForm();
        form.Opacity = 0;
        form.ShowInTaskbar = false;
        form.WindowState = FormWindowState.Minimized;

        TextBox txtExe = (TextBox)GetField(form, "txtExe");
        Button btnScan = (Button)GetField(form, "btnScan");
        ListView list = (ListView)GetField(form, "lvAssets");
        PictureBox preview = (PictureBox)GetField(form, "picOriginal");
        string exe = ResolveSplashStudio.ResolveLocator.Find();
        if (String.IsNullOrEmpty(exe)) { Console.Error.WriteLine("Resolve.exe not found"); return 2; }
        txtExe.Text = exe;

        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        timer.Interval = 300;
        timer.Tick += delegate
        {
            ticks++;
            try
            {
                if (!scanClicked)
                {
                    scanClicked = true;
                    btnScan.PerformClick();
                }
                if (list.Items.Count > 0 && !previewSeen)
                {
                    list.Items[0].Selected = true;
                    list.Focus();
                    if (preview.Image != null)
                    {
                        previewSeen = true;
                        Console.WriteLine("[PASS] UI 扫描显示 " + list.Items.Count + " 个疑似启动图资源");
                        Console.WriteLine("[PASS] UI 选中资源并加载原图预览 " + preview.Image.Width + "x" + preview.Image.Height);
                        timer.Stop();
                        form.Close();
                    }
                }
                if (ticks > 60)
                {
                    failed = true;
                    failure = "UI smoke timeout; list=" + list.Items.Count + ", preview=" + (preview.Image != null);
                    timer.Stop();
                    form.Close();
                }
            }
            catch (Exception ex)
            {
                failed = true;
                failure = ex.ToString();
                timer.Stop();
                form.Close();
            }
        };
        form.Shown += delegate { timer.Start(); };
        Application.Run(form);
        if (failed) { Console.Error.WriteLine(failure); return 1; }
        if (!previewSeen) { Console.Error.WriteLine("Preview not observed"); return 1; }
        Console.WriteLine("UI smoke test passed.");
        return 0;
    }
}

