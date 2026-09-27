using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
public static class UiBatchScreenshot
{
    private static int ticks;
    private static bool started;
    private static object Field(object target,string name){return target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
    private static object Call(object target,string name,params object[] args){return target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}
    [STAThread] public static int Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        MainForm f=new MainForm(); f.StartPosition=FormStartPosition.Manual; f.Location=new Point(-2500,-2500); f.ShowInTaskbar=false; f.Show();
        TextBox txt=(TextBox)Field(f,"txtExe"); Button scan=(Button)Field(f,"btnScan"); ListView list=(ListView)Field(f,"lvAssets"); txt.Text=ResolveSplashStudio.ResolveLocator.Find();
        System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer(); timer.Interval=300;
        timer.Tick += delegate { ticks++; if(!started){started=true;scan.PerformClick();return;} if(list.Items.Count>0){Call(f,"SelectSameSize");Application.DoEvents();using(Bitmap b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save("work\\ui_batch.png",System.Drawing.Imaging.ImageFormat.Png);} timer.Stop(); f.Close();} if(ticks>60){timer.Stop();f.Close();} };
        f.Shown += delegate { timer.Start(); }; Application.Run(f);
        Console.WriteLine("saved batch screenshot"); return 0;
    }
}
