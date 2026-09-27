using System;
using System.Collections.Generic;
using ResolveSplashStudio;
public static class SignatureClearTest {
 public static int Main(){
  try {
   string source="work\\Resolve.patchtest.exe"; string error; PeInfo pe;
   List<PngAsset> assets=PngScanner.Scan(source,2000,out pe,out error);
   PngAsset a=null; for(int i=0;i<assets.Count;i++){if(assets[i].Width==2220&&assets[i].Height==980){a=assets[i];break;}}
   if(a==null) throw new Exception("asset not found");
   byte[] raw=PngCodec.ExtractAsset(source,a); byte[] fit=PngCodec.FitToExactLength(raw,a.Length,out error); if(fit==null) throw new Exception(error);
   PatchItem item=new PatchItem(); item.Original=a; item.OriginalSha256=HashTools.Sha256Bytes(raw); item.Replacement=fit; item.ReplacementSha256=HashTools.Sha256Bytes(fit); item.ReplacementPath="in-memory";
   PatchResult r=Patcher.CreatePatchedCopy(source,"work\\Resolve.clearsig.exe",new List<PatchItem>(){item},true,out error); if(r==null) throw new Exception(error);
   Console.WriteLine("patched checksum=0x"+r.PatchedPeChecksum.ToString("X8")+" verified="+r.ChecksumVerified+" cleared="+r.SignatureDirectoryCleared);
   return 0;
  } catch(Exception ex){Console.Error.WriteLine(ex); return 1;}
 }
}
