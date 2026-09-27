using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using ResolveSplashStudio;

public static class BatchUiSmoke
{
    private static int ticks;
    private static bool started;
    private static bool passed;
    private static string failure = "";

    private static object Field(object target, string name)
    {
        FieldInfo f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) throw new Exception("Missing field: " + name);
        return f.GetValue(target);
    }

    private static object Call(object target, string name, params object[] args)
    {
        MethodInfo m = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (m == null) throw new Exception("Missing method: " + name);
        return m.Invoke(target, args);
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

        TextBox txtExe = (TextBox)Field(form, "txtExe");
        Button scan = (Button)Field(form, "btnScan");
        ListView list = (ListView)Field(form, "lvAssets");
        string exe = ResolveLocator.Find();
        if (String.IsNullOrEmpty(exe)) return 2;
        txtExe.Text = exe;

        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        timer.Interval = 300;
        timer.Tick += delegate
        {
            ticks++;
            try
            {
                if (!started)
                {
                    started = true;
                    scan.PerformClick();
                    return;
                }
                if (list.Items.Count > 0 && !passed)
                {
                    int dragCount = Math.Min(3, list.Items.Count);
                    Point start = new Point(3, list.Items[0].Bounds.Top + 2);
                    Point end = new Point(260, list.Items[dragCount - 1].Bounds.Bottom - 2);
                    Call(form, "OnListMouseDown", new MouseEventArgs(MouseButtons.Left, 1, start.X, start.Y, 0));
                    Call(form, "OnListMouseMove", new MouseEventArgs(MouseButtons.Left, 0, end.X, end.Y, 0));
                    Call(form, "OnListMouseUp", new MouseEventArgs(MouseButtons.Left, 1, end.X, end.Y, 0));
                    if (list.SelectedItems.Count < 2)
                        throw new Exception("Rubber-band selection failed: " + list.SelectedItems.Count);
                    Console.WriteLine("[PASS] 鼠标框选选中 " + list.SelectedItems.Count + " 行");

                    Call(form, "SelectSameSize");
                    List<PngAsset> selected = (List<PngAsset>)Field(form, "selectedAssets");
                    if (selected.Count < 2) throw new Exception("SelectSameSize did not select multiple assets: " + selected.Count);
                    int w = selected[0].Width;
                    int h = selected[0].Height;
                    for (int i = 1; i < selected.Count; i++)
                    {
                        if (selected[i].Width != w || selected[i].Height != h)
                            throw new Exception("Selected assets have different dimensions");
                    }

                    Dictionary<int, byte[]> byLength = new Dictionary<int, byte[]>();
                    string error;
                    for (int i = 0; i < selected.Count; i++)
                    {
                        if (byLength.ContainsKey(selected[i].Length)) continue;
                        byte[] raw = PngCodec.ExtractAsset(exe, selected[i]);
                        byte[] fitted = PngCodec.FitToExactLength(raw, selected[i].Length, out error);
                        if (fitted == null) throw new Exception(error);
                        byLength[selected[i].Length] = fitted;
                    }

                    FieldInfo replBytes = form.GetType().GetField("replacementOriginalBytes", BindingFlags.Instance | BindingFlags.NonPublic);
                    FieldInfo replMap = form.GetType().GetField("replacementByLength", BindingFlags.Instance | BindingFlags.NonPublic);
                    FieldInfo replFirst = form.GetType().GetField("replacementFittedBytes", BindingFlags.Instance | BindingFlags.NonPublic);
                    FieldInfo replPath = form.GetType().GetField("replacementPath", BindingFlags.Instance | BindingFlags.NonPublic);
                    byte[] firstRaw = PngCodec.ExtractAsset(exe, selected[0]);
                    replBytes.SetValue(form, firstRaw);
                    replMap.SetValue(form, byLength);
                    replFirst.SetValue(form, byLength[selected[0].Length]);
                    replPath.SetValue(form, "batch-test");

                    MethodInfo build = form.GetType().GetMethod("BuildPatchItems", BindingFlags.Instance | BindingFlags.NonPublic);
                    object[] args = new object[] { null };
                    List<PatchItem> items = (List<PatchItem>)build.Invoke(form, args);
                    error = args[0] as string;
                    if (items == null) throw new Exception("BuildPatchItems: " + error);
                    if (items.Count < selected.Count) throw new Exception("Batch patch items missing");
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i].Replacement.Length != items[i].Original.Length)
                            throw new Exception("Batch replacement length mismatch");
                    }

                    passed = true;
                    Console.WriteLine("[PASS] Shift/Ctrl/框选使用 MultiSelect 列表");
                    Console.WriteLine("[PASS] 全选同尺寸选中 " + selected.Count + " 张 " + w + "x" + h + " 启动图");
                    Console.WriteLine("[PASS] 批量构建 " + items.Count + " 个精确长度替换项");
                    timer.Stop();
                    form.Close();
                }
                if (ticks > 80)
                {
                    failure = "Batch UI smoke timeout. list=" + list.Items.Count;
                    timer.Stop();
                    form.Close();
                }
            }
            catch (Exception ex)
            {
                failure = ex.ToString();
                timer.Stop();
                form.Close();
            }
        };
        form.Shown += delegate { timer.Start(); };
        Application.Run(form);
        if (passed) return 0;
        Console.Error.WriteLine(failure);
        return 1;
    }
}

