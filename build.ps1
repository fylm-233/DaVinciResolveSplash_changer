$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src\ResolveSplashStudio'
$assets = Join-Path $root 'assets'
$outDir = Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $csc)) {
    throw '找不到 .NET Framework C# 编译器 csc.exe。'
}

$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.IO.Compression.dll',
    'System.IO.Compression.FileSystem.dll'
) -join ','

& $csc /nologo /target:winexe /out:"$outDir\ResolveSplashStudio.exe" /unsafe+ /optimize+ /platform:x64 /reference:$refs "$src\Core.cs" "$src\Gui.cs"
if ($LASTEXITCODE -ne 0) { throw 'GUI 编译失败。' }

& $csc /nologo /target:exe /out:"$outDir\CoreTests.exe" /unsafe+ /optimize+ /platform:x64 /reference:$refs "$src\Core.cs" "$src\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw '核心测试编译失败。' }

& $csc /nologo /target:exe /out:"$outDir\PatchTests.exe" /unsafe+ /optimize+ /platform:x64 /reference:$refs "$src\Core.cs" "$src\PatchTests.cs"
if ($LASTEXITCODE -ne 0) { throw '补丁测试编译失败。' }


& $csc /nologo /target:exe /main:BatchUiSmoke /out:"$outDir\BatchUiSmoke.exe" /unsafe+ /optimize+ /platform:x64 /reference:$refs "$src\Core.cs" "$src\Gui.cs" "$src\BatchUiSmoke.cs"
if ($LASTEXITCODE -ne 0) { throw '批量 GUI 测试编译失败。' }

& $csc /nologo /target:exe /out:"$outDir\BatchPatchTest.exe" /unsafe+ /optimize+ /platform:x64 /reference:$refs "$src\Core.cs" "$src\BatchPatchTest.cs"
if ($LASTEXITCODE -ne 0) { throw '批量补丁测试编译失败。' }

Write-Host '构建完成：' $outDir

