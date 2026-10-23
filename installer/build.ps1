# Construit l'installeur : publie l'exe autonome, puis compile installer\MinecraftServerMaker.iss.
# Résultat : publish\MinecraftServerMaker-Setup-<version>.exe
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$project = Join-Path $root 'src\MinecraftServerMaker'

dotnet publish $project -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o (Join-Path $root 'publish')
if ($LASTEXITCODE -ne 0) { throw 'La publication a échoué.' }

$version = (dotnet msbuild $project -getProperty:Version).Trim()
$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 est introuvable : https://jrsoftware.org/isdl.php' }

& $iscc "/DAppVersion=$version" (Join-Path $PSScriptRoot 'MinecraftServerMaker.iss')
if ($LASTEXITCODE -ne 0) { throw "La compilation de l'installeur a échoué." }
