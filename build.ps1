$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src'
$assets = Join-Path $root 'assets'
$dist = Join-Path $root 'dist'
$out = Join-Path $dist 'SkyMatch.exe'

$cscCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $null
foreach ($candidate in $cscCandidates) {
    if (Test-Path $candidate) { $csc = $candidate; break }
}
if (!$csc) { throw 'The .NET Framework C# compiler (csc.exe) was not found.' }
if (!(Test-Path $assets)) { New-Item -ItemType Directory -Path $assets | Out-Null }
if (!(Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }

$icon = Join-Path $assets 'app.ico'
if (!(Test-Path $icon)) {
    & (Join-Path $root 'tools\make-icon.ps1')
}

$files = @(Get-ChildItem -Path $src -Recurse -Filter *.cs | Sort-Object FullName | ForEach-Object { $_.FullName })
$ref48 = Join-Path $root 'lib\ref48'
$lib = Join-Path $root 'lib'
$references = @('/noconfig', '/nostdlib+')
if (Test-Path $ref48) {
    $references += @(Get-ChildItem -Path (Join-Path $ref48 '*.dll') | ForEach-Object { '/r:' + $_.FullName })
    $netstandard = Join-Path $ref48 'Facades\netstandard.dll'
    if (Test-Path $netstandard) { $references += ('/r:' + $netstandard) }
} else {
    $references += @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll',
        '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll', '/r:System.Net.Http.dll')
}
if (Test-Path $lib) {
    $references += @(Get-ChildItem -Path (Join-Path $lib '*.dll') |
        Where-Object { $_.Name -ne 'onnxruntime.dll' } |
        ForEach-Object { '/r:' + $_.FullName })
}

Write-Host ("Building SkyMatch from {0} source files..." -f $files.Count)
$arguments = @(
    '/nologo',
    '/langversion:5',
    '/codepage:65001',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/debug-',
    '/main:SkyMatch.Program',
    ('/out:' + $out),
    ('/win32icon:' + $icon),
    ('/win32manifest:' + (Join-Path $src 'app.manifest'))
) + $references + $files

& $csc $arguments
if ($LASTEXITCODE -ne 0) { throw ('Compilation failed with exit code ' + $LASTEXITCODE) }

Copy-Item (Join-Path $src 'SkyMatch.exe.config') (Join-Path $dist 'SkyMatch.exe.config') -Force
if (Test-Path $lib) {
    Get-ChildItem -Path (Join-Path $lib '*.dll') | ForEach-Object { Copy-Item $_.FullName $dist -Force }
}
$netstandard = Join-Path $ref48 'Facades\netstandard.dll'
if (Test-Path $netstandard) { Copy-Item $netstandard $dist -Force }
$models = Join-Path $root 'models'
if (Test-Path $models) {
    foreach ($modelName in @('clip-large')) {
        $modelSource = Join-Path $models $modelName
        if (!(Test-Path $modelSource)) { continue }
        $modelTarget = Join-Path $dist ('models\' + $modelName)
        New-Item -ItemType Directory -Force -Path $modelTarget | Out-Null
        $modelFiles = @('vision_model.onnx', 'prompt_embeddings.json', 'config.json', 'preprocessor_config.json')
        foreach ($name in $modelFiles) {
            $source = Join-Path $modelSource $name
            if (Test-Path $source) { Copy-Item $source $modelTarget -Force }
        }
    }
    $yoloSource = Join-Path $models 'yolo\yolov8n.onnx'
    if (Test-Path $yoloSource) {
        $yoloTarget = Join-Path $dist 'models\yolo'
        New-Item -ItemType Directory -Force -Path $yoloTarget | Out-Null
        Copy-Item $yoloSource $yoloTarget -Force
    }
}
$info = Get-Item $out
Write-Host ("Built {0} ({1:N0} bytes)" -f $info.FullName, $info.Length)
Write-Host ("Version: " + $info.VersionInfo.FileVersion)
