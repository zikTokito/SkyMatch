param(
    [string]$PortableZip = "",
    [string]$Destination = "",
    [switch]$SkipHashCheck
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $root "models"
}

function Copy-ModelsFromDirectory([string]$source) {
    if (!(Test-Path $source)) { throw "Models directory not found: $source" }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Copy-Item (Join-Path $source "*") $Destination -Recurse -Force
    Write-Host "Copied models from $source"
}

if ([string]::IsNullOrWhiteSpace($PortableZip)) {
    $installed = Join-Path $env:LOCALAPPDATA "Programs\SkyMatch\models"
    if (Test-Path $installed) {
        Copy-ModelsFromDirectory $installed
    } else {
        throw "Pass -PortableZip <SkyMatch-1.2.0-Portable.zip>, or install SkyMatch first."
    }
} else {
    $zipPath = (Resolve-Path $PortableZip).Path
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $count = 0
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace("/", "\")
            if (!$name.StartsWith("models\", [System.StringComparison]::OrdinalIgnoreCase)) { continue }
            if ($name.EndsWith("\")) { continue }
            $target = Join-Path $Destination $name.Substring(7)
            $parent = Split-Path -Parent $target
            New-Item -ItemType Directory -Force -Path $parent | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
            $count++
        }
        if ($count -eq 0) { throw "No models/ entries found in $zipPath" }
        Write-Host "Extracted $count model file(s) to $Destination"
    } finally {
        $archive.Dispose()
    }
}

# prompt_embeddings.json is small enough to keep in the source repository.
$prompt = Join-Path $Destination "clip-large\prompt_embeddings.json"
$repoPrompt = Join-Path $root "assets\prompt_embeddings.json"
if (!(Test-Path $prompt) -and (Test-Path $repoPrompt)) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $prompt) | Out-Null
    Copy-Item $repoPrompt $prompt -Force
    Write-Host "Copied prompt_embeddings.json from assets/"
}

$expected = @{
    "clip-large\vision_model.onnx" = "9A658DE8944A2001719BEABF6C5030CFE7B3DC80E85983F69FC648D6B4D7EC0F"
    "clip-large\prompt_embeddings.json" = "AD129B75FDB9670A91837D94D2D043A707486028336B9B57DF144E9D42799161"
    "yolo\yolov8n.onnx" = "E0BD17E5CB5AECCBB0CF89C9BF8B8425C5BF36F835CCD323DC8BEEB235C9CE23"
}
if (!$SkipHashCheck) {
    foreach ($relative in $expected.Keys) {
        $file = Join-Path $Destination $relative
        if (!(Test-Path $file)) { throw "Missing model file: $file" }
        $hash = (Get-FileHash $file -Algorithm SHA256).Hash
        if ($hash -ne $expected[$relative]) {
            throw "SHA-256 mismatch: $relative`n  expected $($expected[$relative])`n  actual   $hash"
        }
    }
    Write-Host "Model hashes OK."
}

Write-Host "Done. Build with: .\build.cmd"
