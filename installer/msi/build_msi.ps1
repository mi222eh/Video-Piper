param(
    [string]$PublishDir = "D:\projects\github\Video-Piper\video-piper\publish",
    [string]$WixDir = "C:\Users\micke\tools\wix311",
    [string]$OutDir = "D:\projects\github\Video-Piper\installer\output"
)
$ErrorActionPreference = "Stop"
$m = Split-Path -Parent $MyInvocation.MyCommand.Path

# 1) heat the publish dir (fragments)
$heat = Join-Path $m "VideoPiper_heat.wxs"
Write-Output "heat: harvesting $PublishDir ..."
& (Join-Path $WixDir "heat.exe") dir $PublishDir -gg -nolog -var "var.PUBLISH" -out $heat 2>$null
if ($LASTEXITCODE -ne 0) { throw "heat failed ($LASTEXITCODE)" }

# 2) inner fragments (strip XML decl + Wix wrapper)
$raw = Get-Content $heat -Raw
$frag = [regex]::Replace($raw, '^\s*<\?xml[^>]*\?>\s*', '')
$frag = [regex]::Replace($frag, '<Wix [^>]*>', '')
$frag = [regex]::Replace($frag, '</Wix>\s*$', '')
# Fragments are Wix-level wrappers, not allowed inside Product: strip them
$frag = [regex]::Replace($frag, '<Fragment>', '')
$frag = [regex]::Replace($frag, '</Fragment>', '')

# 3a) strip COM registration artifacts that heat harvested from .NET assembly
#     metadata (Class/ProgId + HKCR CLSID values). A self-contained .NET app
#     does no COM registration; HKCR is per-machine data that breaks per-user
#     installs (ICE57) and would pollute the registry.
$before = $frag.Length
$frag = [regex]::Replace($frag, '<Class [^>]*>.*?</Class>', '', 'Singleline')
$frag = [regex]::Replace($frag, '<ProgId [^>]*/>', '')
$frag = [regex]::Replace($frag, '<RegistryValue Root="HKCR"[^>]*/>', '', 'Singleline')
Write-Output ("stripped COM artifacts: {0} chars" -f ($before - $frag.Length))

# 3) drop the first heat fragment (it re-declares the root dir under TARGETDIR;
#    our template already defines INSTALLDIR under LocalAppData\Programs)
$m1 = [regex]::Match($frag, '<DirectoryRef Id="TARGETDIR">\s*<Directory Id="([^"]+)" Name="publish" />\s*</DirectoryRef>')
if (-not $m1.Success) { throw "could not find heat root directory fragment" }
$rootId = $m1.Groups[1].Value
Write-Output "heat root dir id: $rootId"
$frag = $frag.Replace($m1.Value, '')
$frag = $frag.Replace($rootId, 'INSTALLDIR')

# 4) component refs
$ids = [regex]::Matches($frag, '<Component Id="(cmp[0-9A-Fa-f]+)"') | ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique
Write-Output "components: $($ids.Count)"
$refs = ($ids | ForEach-Object { "      <ComponentRef Id=""$_"" />" }) -join "`n"

# 5) assemble final wxs
$tpl = Get-Content (Join-Path $m "VideoPiper_main.wxs") -Raw
$final = $tpl.Replace('<!-- HEAT_FRAGMENTS -->', $frag)
$final = $final.Replace('<!-- COMPONENT_REFS -->', $refs)
$finalPath = Join-Path $m "VideoPiper_final.wxs"
Set-Content -Path $finalPath -Value $final -Encoding UTF8

# 6) candle (PUBLISH resolved at compile time => self-contained MSI)
Write-Output "candle ..."
& (Join-Path $WixDir "candle.exe") -arch x64 -dPUBLISH="$PublishDir" -out (Join-Path $m "VideoPiper_final.wixobj") $finalPath
if ($LASTEXITCODE -ne 0) { throw "candle failed ($LASTEXITCODE)" }

# 7) light
$msi = Join-Path $OutDir "VideoPiper-1.0.0-win-x64.msi"
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory $OutDir | Out-Null }
Write-Output "light -> $msi"
# NOTE: light 3.x has no -arch (the arch comes from candle's wixobj) and no -nolog.
# ICE38/ICE64/ICE91 are inherent to per-user profile installs (all heat components
# are files under %LOCALAPPDATA% with file keypaths) - suppressed as standard practice.
# ICE03: File.Language is auto-filled by light from each file's own VERSIONINFO
# language tag (MUI files carry unusual culture ids like Gaelic 512); purely
# informational column, no install impact.
# ICE17: the ICE runner itself errors ("ICE Internal Error 105", Error 2228) when
# it queries the optional ControlCondition table, which doesn't exist because this
# UI uses no <Condition> control children (all wiring is via <Publish>/ControlEvent).
& (Join-Path $WixDir "light.exe") -nologo -cultures:sv-SE -sice:ICE03 -sice:ICE17 -sice:ICE38 -sice:ICE64 -sice:ICE91 -out $msi -dPUBLISH="$PublishDir" (Join-Path $m "VideoPiper_final.wixobj")
if ($LASTEXITCODE -ne 0) { throw "light failed ($LASTEXITCODE)" }

$fi = Get-Item $msi
Write-Output "OK: $($fi.FullName) ($([math]::Round($fi.Length/1MB,1)) MB)"
