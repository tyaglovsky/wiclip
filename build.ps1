<#
.SYNOPSIS
    Сборка WiClip: публикация приложения и создание MSI-пакета.

.DESCRIPTION
    Запускать на Windows. Требуется:
      * .NET SDK 8.0+            https://dotnet.microsoft.com/download
      * WiX Toolset 5 (dotnet tool install --global wix)

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Version 1.2.0 -Culture en-US
    .\build.ps1 -SkipMsi          # только exe, без установщика
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [ValidateSet("x64", "x86", "arm64")]
    [string]$Arch = "x64",
    [string]$Configuration = "Release",
    [string]$Culture = "ru-RU",
    [switch]$SkipMsi
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\WiClip\WiClip.csproj"
$publishDir = Join-Path $root "publish\$Arch"
$installerDir = Join-Path $root "installer"
$distDir = Join-Path $root "dist"
$generatedWxs = Join-Path $installerDir "Files.generated.wxs"

# MSI требует версию вида a.b.c.d
$msiVersion = if ($Version -match '^\d+\.\d+\.\d+\.\d+$') { $Version } else { "$Version.0" }

function Write-Step($text) { Write-Host "`n==> $text" -ForegroundColor Cyan }

# ---------------------------------------------------------------- publish ---
Write-Step "Публикация приложения ($Arch, $Configuration)"

# Запущенный экземпляр держит свои файлы и ломает публикацию
$running = Get-Process -Name WiClip -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Закрываю запущенный WiClip (PID $($running.Id -join ', '))..."
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish $project `
    -c $Configuration `
    -r "win-$Arch" `
    --self-contained true `
    -p:Version=$Version `
    -p:FileVersion=$msiVersion `
    -p:AssemblyVersion=$msiVersion `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish завершился с ошибкой." }

# Отладочные символы в установщик не кладём
Get-ChildItem $publishDir -Filter *.pdb -Recurse | Remove-Item -Force

$exe = Join-Path $publishDir "WiClip.exe"
if (-not (Test-Path $exe)) { throw "WiClip.exe не найден в $publishDir" }

$fileCount = (Get-ChildItem $publishDir -Recurse -File).Count
$sizeMb = [math]::Round(((Get-ChildItem $publishDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "Готово: $fileCount файлов, $sizeMb МБ -> $publishDir"

if ($SkipMsi) {
    Write-Host "`nMSI пропущен (-SkipMsi). Приложение: $exe" -ForegroundColor Green
    return
}

# ------------------------------------------------- генерация списка файлов ---
Write-Step "Генерация Files.generated.wxs"

$sb = [System.Text.StringBuilder]::new()
$null = $sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
$null = $sb.AppendLine('<!-- Файл создаётся автоматически build.ps1. Правки будут перезаписаны. -->')
$null = $sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
$null = $sb.AppendLine('  <Fragment>')

# Каталоги (подпапки publish повторяем внутри APPLICATIONFOLDER)
$dirIds = @{ "" = "APPLICATIONFOLDER" }
$subDirs = Get-ChildItem $publishDir -Recurse -Directory | Sort-Object FullName
if ($subDirs) {
    $null = $sb.AppendLine('    <DirectoryRef Id="APPLICATIONFOLDER">')
    $open = New-Object System.Collections.Stack
    foreach ($dir in $subDirs) {
        $rel = $dir.FullName.Substring($publishDir.Length).TrimStart('\')
        $id = "dir_" + ($rel -replace '[^A-Za-z0-9_]', '_')
        $dirIds[$rel] = $id

        # Закрываем каталоги, которые больше не являются родителями текущего
        while ($open.Count -gt 0 -and -not $rel.StartsWith($open.Peek() + '\')) {
            $null = $open.Pop()
            $null = $sb.AppendLine(('      ' + ('  ' * $open.Count) + '</Directory>'))
        }
        $indent = '      ' + ('  ' * $open.Count)
        $null = $sb.AppendLine("$indent<Directory Id=`"$id`" Name=`"$($dir.Name)`">")
        $open.Push($rel)
    }
    while ($open.Count -gt 0) {
        $null = $open.Pop()
        $null = $sb.AppendLine(('      ' + ('  ' * $open.Count) + '</Directory>'))
    }
    $null = $sb.AppendLine('    </DirectoryRef>')
}

# Компоненты: по одному файлу на компонент (правило MSI — один keypath на компонент)
$null = $sb.AppendLine('    <ComponentGroup Id="AppFiles">')
$index = 0
foreach ($file in (Get-ChildItem $publishDir -Recurse -File | Sort-Object FullName)) {
    $rel = $file.FullName.Substring($publishDir.Length).TrimStart('\')
    $relDir = Split-Path $rel -Parent
    $dirId = $dirIds[$relDir]
    if (-not $dirId) { throw "Не найден каталог для $rel" }

    # У главного exe фиксированный Id — на него ссылается CustomAction в WiClip.wxs
    if ($rel -eq "WiClip.exe") {
        $fileId = "WiClipExe"
        $compId = "cmp_WiClipExe"
    }
    else {
        $index++
        $fileId = "fil_$index"
        $compId = "cmp_$index"
    }

    $source = [System.Security.SecurityElement]::Escape($file.FullName)
    $null = $sb.AppendLine("      <Component Id=`"$compId`" Directory=`"$dirId`">")
    $null = $sb.AppendLine("        <File Id=`"$fileId`" Source=`"$source`" KeyPath=`"yes`" />")
    $null = $sb.AppendLine('      </Component>')
}
$null = $sb.AppendLine('    </ComponentGroup>')
$null = $sb.AppendLine('  </Fragment>')
$null = $sb.AppendLine('</Wix>')

Set-Content -Path $generatedWxs -Value $sb.ToString() -Encoding UTF8
Write-Host "Записано: $generatedWxs"

# -------------------------------------------------------------------- msi ---
Write-Step "Сборка MSI"

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    throw "Не найден WiX. Установите его: dotnet tool install --global wix"
}

# Версия расширений должна совпадать с версией самого wix (у 4/5/6/7 они разные)
$wixVersion = $null
$raw = (& wix --version) 2>$null
if ("$raw" -match '(\d+\.\d+\.\d+)') { $wixVersion = $Matches[1] }
Write-Host "WiX версии $(if ($wixVersion) { $wixVersion } else { 'неизвестной' })"

# WiX 6 и новее требуют принять Open Source Maintenance Fee EULA (ошибка WIX7015)
if ($wixVersion -and [int]($wixVersion.Split('.')[0]) -ge 6) {
    Write-Warning @"
Установлен WiX $wixVersion. Начиная с версии 6 он требует принять OSMF EULA
(https://wixtoolset.org/osmf/), иначе выдаёт ошибку WIX7015.
Проще откатиться на свободную версию 5:
    dotnet tool uninstall --global wix
    dotnet tool install --global wix --version 5.0.2
"@
}

foreach ($ext in @("WixToolset.UI.wixext", "WixToolset.Util.wixext")) {
    $spec = if ($wixVersion) { "$ext/$wixVersion" } else { $ext }
    Write-Host "Расширение: $spec"
    & wix extension add -g $spec
    if ($LASTEXITCODE -ne 0 -and $wixVersion) {
        # Версии «в тон» может не оказаться в NuGet — пробуем последнюю совместимую
        & wix extension add -g $ext
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Не удалось добавить расширение $ext. Проверьте доступ в интернет (NuGet) " +
              "и выполните вручную: wix extension add -g $spec"
    }
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
$msi = Join-Path $distDir "WiClip-$Version-$Arch.msi"

wix build `
    (Join-Path $installerDir "WiClip.wxs") `
    $generatedWxs `
    -arch $Arch `
    -culture $Culture `
    -ext WixToolset.UI.wixext `
    -ext WixToolset.Util.wixext `
    -d Version=$msiVersion `
    -d PublishDir=$publishDir `
    -d InstallerDir=$installerDir `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw "wix build завершился с ошибкой." }

$msiMb = [math]::Round((Get-Item $msi).Length / 1MB, 1)
Write-Host "`nГотово: $msi ($msiMb МБ)" -ForegroundColor Green
Write-Host "Тихая установка:  msiexec /i `"$msi`" /qn" -ForegroundColor DarkGray
