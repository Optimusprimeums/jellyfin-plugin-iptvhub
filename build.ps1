# Builds the plugin and produces an installable zip in ./dist
param([string]$Version = "1.2.1.0", [string]$Changelog = "")
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "dist\IptvHub_$Version"

dotnet publish "$root\Jellyfin.Plugin.IptvHub\Jellyfin.Plugin.IptvHub.csproj" -c Release -o "$root\dist\publish" -p:Version=$Version -p:AssemblyVersion=$Version -p:FileVersion=$Version
if ($LASTEXITCODE -ne 0) { throw "build failed" }

Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item "$root\dist\publish\Jellyfin.Plugin.IptvHub.dll" $out

@{
    category    = "Live TV"
    changelog   = $Changelog
    description = "Serve IPTV from M3U playlists (URL, server path or upload) with XMLTV guides, playlist categories and your own custom categories."
    guid        = "7c1f6a52-3e1b-4c8e-9a0d-5b2f4e8d9c31"
    name        = "IPTV Hub"
    overview    = "M3U + XMLTV IPTV with categories"
    owner       = "local"
    targetAbi   = "12.4.0.0"
    timestamp   = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    version     = $Version
    status      = "Active"
    autoUpdate  = $false
    assemblies  = @("Jellyfin.Plugin.IptvHub.dll")
} | ConvertTo-Json | Set-Content (Join-Path $out "meta.json") -Encoding UTF8

$zip = Join-Path $root "dist\IptvHub_$Version.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path "$out\*" -DestinationPath $zip
Remove-Item "$root\dist\publish" -Recurse -Force
Write-Host "Plugin folder: $out"
Write-Host "Zip:           $zip"
Write-Host "MD5 (for manifest.json checksum): $((Get-FileHash $zip -Algorithm MD5).Hash.ToLowerInvariant())"
