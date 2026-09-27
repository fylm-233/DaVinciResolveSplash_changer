using System;
using System.Collections.Generic;
using ResolveSplashStudio;
public static class BatchPatchTest
{
    public static int Main()
    {
        try
        {
            string source=ResolveLocator.Find(), error; PeInfo pe;
            List<PngAsset> assets=PngScanner.Scan(source,2000,out pe,out error);
            List<PatchItem> items=new List<PatchItem>();
            for(int i=0;i<assets.Count;i++)
            {
                PngAsset a=assets[i];
                if(a.Width!=2220||a.Height!=980) continue;
                byte[] raw=PngCodec.ExtractAsset(source,a);
                byte[] fitted=PngCodec.FitToExactLength(raw,a.Length,out error);
                if(fitted==null) throw new Exception(error);
                PatchItem item=new PatchItem();
                item.Original=a; item.OriginalSha256=HashTools.Sha256Bytes(raw); item.Replacement=fitted; item.ReplacementSha256=HashTools.Sha256Bytes(fitted); item.ReplacementPath="batch-self-test";
                items.Add(item);
            }
            if(items.Count!=19) throw new Exception("Expected 19 2220x980 assets, got "+items.Count);
            PatchResult r=Patcher.CreatePatchedCopy(source,"work\\Resolve.batchpatch.exe",items,false,out error);
            if(r==null) throw new Exception(error);
            if(!r.LengthPreserved||!r.ChecksumVerified||r.ReplacementCount!=19) throw new Exception("Batch patch verification failed");
            uint header; uint checksum=PeTools.ComputeChecksum(r.OutputPath,out header,out error);
            if(!String.IsNullOrEmpty(error)||checksum!=r.PatchedPeChecksum) throw new Exception("Independent checksum failed: "+error);
            Console.WriteLine("[PASS] 单次批补丁写入 "+r.ReplacementCount+" 个资源");
            Console.WriteLine("[PASS] 文件长度保持 "+r.OutputSize.ToString("N0")+" 字节");
            Console.WriteLine("[PASS] PE Checksum 复核通过 0x"+r.PatchedPeChecksum.ToString("X8"));
            return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}
    }
}
