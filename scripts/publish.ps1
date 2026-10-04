# 统一发布入口：scripts/publish.ps1
# 用法：pwsh scripts/publish.ps1 [-RuntimeIdentifier win-x64] [-Version 0.0.0]
# 输出：artifacts/publish/<rid>/<AppName>/
[CmdletBinding()]
param(
    [string] $RuntimeIdentifier = "win-x64",
    [string] $Version = ""
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($Version)) {
    $props = Join-Path $repositoryRoot "Directory.Build.props"
    if (Test-Path -LiteralPath $props) {
        $match = Select-String -LiteralPath $props -Pattern '<Version>([^<]+)</Version>' |
            Select-Object -First 1
        if ($match) { $Version = $match.Matches[0].Groups[1].Value }
    }
}
Write-Host "发布 $RuntimeIdentifier (Version=$Version)"

$tfm = if ($RuntimeIdentifier.StartsWith("win-", [StringComparison]::OrdinalIgnoreCase)) { "net10.0-windows" } else { "net10.0" }
# win-x64 / Linux / macOS 走 NativeAOT（完整反射元数据保全）；win-x86 不受 NativeAOT 支持，保持自包含单文件；
# macOS 保留符号（ld_classic 不支持压缩调试段）
$aotArgs = @()
$singleFile = "true"
$trimmed = "false"
if ($RuntimeIdentifier -ne "win-x86") {
    $singleFile = "false"
    $trimmed = "true"
    $aotArgs = @("-p:PublishAot=true",
        "-p:IlcGenerateCompleteTypeMetadata=true", "-p:IlcTrimMetadata=false", "-p:IlcSingleThreaded=true")
    if ($RuntimeIdentifier.StartsWith("osx-", [StringComparison]::OrdinalIgnoreCase)) { $aotArgs += "-p:StripSymbols=false" }
}
dotnet publish (Join-Path $repositoryRoot "src/KidWall.App/KidWall.App.csproj") -c Release -f $tfm -r $RuntimeIdentifier --self-contained true @aotArgs -p:PublishSingleFile=$singleFile -p:PublishTrimmed=$trimmed -p:DebugType=none -p:DebugSymbols=false -p:Version=$Version -o (Join-Path $repositoryRoot "artifacts/publish/$RuntimeIdentifier/KidWall.App")
if ($LASTEXITCODE -ne 0) { throw "publish failed for $RuntimeIdentifier" }
