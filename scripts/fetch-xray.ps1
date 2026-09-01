param([string]$Version)

$root = Split-Path -Parent $PSScriptRoot
$config = Get-Content -Raw (Join-Path $root "deploy/xray-core.json") | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($Version))
{
    $Version = $config.version
}
if ($Version -notmatch '^v\d+\.\d+\.\d+$')
{
    throw "Некорректная версия Xray: $Version"
}

function Get-XrayCore([string]$Rid, [string]$Asset)
{
    $directory = Join-Path $root "artifacts/xray-core/$Version/$Rid"
    $url = "https://github.com/XTLS/Xray-core/releases/download/$Version/$Asset"
    $archive = Join-Path $directory $Asset
    $digest = "$archive.dgst"
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    Invoke-WebRequest -Uri $url -OutFile $archive
    Invoke-WebRequest -Uri "$url.dgst" -OutFile $digest
    $expected = ([regex]::Match((Get-Content -Raw $digest), 'SHA2-256=\s*([0-9a-fA-F]{64})')).Groups[1].Value.ToLowerInvariant()
    $actual = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($expected -ne $actual)
    {
        Remove-Item -LiteralPath $archive, $digest -Force -ErrorAction SilentlyContinue
        throw "Контрольная сумма Xray не совпала: $Rid"
    }
    [IO.File]::WriteAllText("$archive.sha256", "$actual`n", [Text.UTF8Encoding]::new($false))
    Remove-Item -LiteralPath $digest -Force
}

Get-XrayCore "linux-x64" $config.assets.'linux-x64'
Get-XrayCore "linux-arm64" $config.assets.'linux-arm64'
