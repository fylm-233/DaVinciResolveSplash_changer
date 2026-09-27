using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ResolveSplashStudio;

public static class PatchTests
{
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("ASSERT FAILED: " + message);
        Console.WriteLine("[PASS] " + message);
    }

    public static int Main()
    {
        try
        {
            string exe = ResolveLocator.Find();
            PeInfo pe; string error;
            List<PngAsset> assets = PngScanner.Scan(exe, 2000, out pe, out error);
            Assert(assets != null && assets.Count > 1, "扫描高分辨率启动图候选");
            PngAsset a = null, b = null;
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i].Width != 2220 || assets[i].Height != 980) continue;
                if (a == null) a = assets[i];
                else if (assets[i].Offset != a.Offset) { b = assets[i]; break; }
            }
            Assert(a != null && b != null, "选择两个同为 2220x980 的不同资源");
            byte[] replacement = PngCodec.ExtractAsset(exe, b);
            replacement = PngCodec.FitToExactLength(replacement, a.Length, out error);
            Assert(replacement != null, "把第二个启动图精确适配到第一个资源长度: " + error);

            PatchItem item = new PatchItem();
            item.Original = a;
            item.OriginalSha256 = HashTools.Sha256Bytes(PngCodec.ExtractAsset(exe, a));
            item.Replacement = replacement;
            item.ReplacementSha256 = HashTools.Sha256Bytes(replacement);
            item.ReplacementPath = "in-memory:asset_" + b.Index;

            string output = Path.GetFullPath("work\\Resolve.patchtest.exe");
            Stopwatch sw = Stopwatch.StartNew();
            PatchResult result = Patcher.CreatePatchedCopy(exe, output, new List<PatchItem>() { item }, false, out error);
            sw.Stop();
            Assert(result != null, "生成精确补丁副本: " + error);
            Assert(result.LengthPreserved, "输出与原始 Resolve.exe 长度一致");
            Assert(result.ChecksumVerified, "输出 PE Checksum 已重新计算并复核");
            Assert(File.Exists(result.ManifestPath), "生成 JSON 清单");
            Console.WriteLine("  patch elapsed=" + sw.ElapsedMilliseconds + " ms");
            Console.WriteLine("  PE checksum 0x" + result.OriginalPeChecksum.ToString("X8") + " -> 0x" + result.PatchedPeChecksum.ToString("X8"));

            uint directHeader;
            uint directChecksum = PeTools.ComputeChecksum(output, out directHeader, out error);
            Assert(String.IsNullOrEmpty(error), "独立读取输出 PE Checksum");
            Assert(directChecksum == result.PatchedPeChecksum, "输出文件头部 PE Checksum 与清单一致");
            byte[] actual = PngCodec.ExtractAsset(output, a);
            Assert(actual.Length == replacement.Length, "输出资源长度一致");
            Assert(HashTools.Sha256Bytes(actual) == item.ReplacementSha256, "输出资源 SHA-256 与替换数据一致");
            PngPacket packet = PngCodec.Parse(actual, true, out error);
            Assert(packet != null && packet.Width == 2220 && packet.Height == 980, "输出资源是有效 PNG 且尺寸保持不变");

            Console.WriteLine("Patch tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }
}
