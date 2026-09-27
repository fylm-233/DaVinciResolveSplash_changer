using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using ResolveSplashStudio;

public static class CoreTests
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("ASSERT FAILED: " + message);
        Console.WriteLine("[PASS] " + message);
    }

    public static int Main(string[] args)
    {
        try
        {
            Console.WriteLine("Resolve Splash Studio core test");
            string exe = ResolveLocator.Find();
            Assert(!String.IsNullOrEmpty(exe), "自动定位 Resolve.exe");
            PeInfo pe;
            string error;
            PeInfo direct = PeTools.Parse(exe, out error);
            Assert(direct != null, "解析 PE 头");
            uint header;
            uint calculated = PeTools.ComputeChecksum(exe, out header, out error);
            Assert(String.IsNullOrEmpty(error), "调用 Windows PE Checksum API");
            Assert(calculated == direct.StoredChecksum, "现有 Resolve.exe PE Checksum 与 API 结果一致");
            Console.WriteLine("  stored=0x" + direct.StoredChecksum.ToString("X8") + " calculated=0x" + calculated.ToString("X8"));

            Stopwatch sw = Stopwatch.StartNew();
            List<PngAsset> assets = PngScanner.Scan(exe, 500, out pe, out error);
            sw.Stop();
            Assert(assets != null, "扫描嵌入 PNG");
            Assert(assets.Count >= 100, "找到 500px 以上的候选 PNG 数量: " + assets.Count);
            Console.WriteLine("  scan elapsed=" + sw.ElapsedMilliseconds + " ms");
            PngAsset source = null;
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i].Width == 2220 && assets[i].Height == 980)
                {
                    source = assets[i];
                    break;
                }
            }
            Assert(source != null, "找到 2220x980 启动图候选");
            byte[] original = PngCodec.ExtractAsset(exe, source);
            Assert(original.Length == source.Length, "提取资源长度正确");
            string pngError;
            PngPacket packet = PngCodec.Parse(original, true, out pngError);
            Assert(packet != null, "提取 PNG 全 chunk CRC 校验通过: " + pngError);
            Assert(packet.Width == source.Width && packet.Height == source.Height, "提取 PNG 尺寸正确");

            byte[] fitted = PngCodec.FitToExactLength(original, original.Length, out error);
            Assert(fitted != null, "将原 PNG 重建为完全相同的目标长度: " + error);
            Assert(fitted.Length == original.Length, "重建后长度完全一致");
            PngPacket fittedPacket = PngCodec.Parse(fitted, true, out error);
            Assert(fittedPacket != null, "重建 PNG 全 chunk CRC 校验通过: " + error);

            Console.WriteLine("Core tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }
}
