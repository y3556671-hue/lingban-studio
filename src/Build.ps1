$ErrorActionPreference = 'Stop'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw '需要 Windows .NET Framework 4.x 编译器。' }
$frameworkPath = Split-Path -Parent $compilerPath
$wpfPath = Join-Path $frameworkPath 'WPF'
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Runtime.Serialization.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Xaml.dll',"/r:$wpfPath\WindowsBase.dll","/r:$wpfPath\PresentationCore.dll","/r:$wpfPath\PresentationFramework.dll")
$sourceFiles = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object { $_.FullName }
$outputPath = Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$exePath = Join-Path $outputPath 'Lingban.exe'
& $compilerPath /nologo /target:winexe /optimize+ /codepage:65001 "/out:$exePath" @references @sourceFiles
if ($LASTEXITCODE -ne 0) { throw '编译未成功。' }
Write-Output "已生成：$exePath"
