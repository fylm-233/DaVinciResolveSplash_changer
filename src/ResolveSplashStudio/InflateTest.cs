using System;
using System.Collections.Generic;
using System.Reflection;
using ResolveSplashStudio;
public static class InflateTest {
 public static int Main(){
  try {
   string exe=ResolveLocator.Find(), error; PeInfo pe; List<PngAsset> a=PngScanner.Scan(exe,2000,out pe,out error); PngAsset x=null; for(int i=0;i<a.Count;i++){if(a[i].Width==2220&&a[i].Height==980){x=a[i];break;}}
   byte[] raw=PngCodec.ExtractAsset(exe,x); PngPacket p=PngCodec.Parse(raw,true,out error);
   MethodInfo mi=typeof(PngCodec).GetMethod("TryReadInflated",BindingFlags.NonPublic|BindingFlags.Static);
   object[] args=new object[]{p,null,null}; bool ok=(bool)mi.Invoke(null,args); error=(string)args[2];
   if(!ok) throw new Exception(error);
   byte[] inflated=(byte[])args[1];
   Console.WriteLine("[PASS] DeflateStream 解出原始扫描线数据 " + inflated.Length + " bytes");
   MethodInfo ci=typeof(PngCodec).GetMethod("CompressInflatedZlib",BindingFlags.NonPublic|BindingFlags.Static);
   byte[] z=(byte[])ci.Invoke(null,new object[]{inflated,System.IO.Compression.CompressionLevel.Optimal});
   PngPacket q=PngCodec.Parse(raw,true,out error);
   MethodInfo bi=typeof(PngCodec).GetMethod("BuildWithIdat",BindingFlags.NonPublic|BindingFlags.Static);
   byte[] rebuilt=(byte[])bi.Invoke(null,new object[]{q,z,1,0});
   PngPacket parsed=PngCodec.Parse(rebuilt,true,out error); if(parsed==null) throw new Exception(error);
   object[] check=new object[]{parsed,null,null}; bool checkOk=(bool)mi.Invoke(null,check);
   if(!checkOk) throw new Exception((string)check[2]);
   Console.WriteLine("[PASS] 重压缩 zlib 流可再次完整解压，长度="+z.Length);
   return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
 }
}
