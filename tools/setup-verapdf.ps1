<#
.SYNOPSIS
    Installs veraPDF and a Java runtime under tools\, for the optional PDF/A
    conformance pass.

.DESCRIPTION
    The validator's built-in checks cover the Factur-X / ZUGFeRD structure, the
    XMP metadata and the PDF-to-XML relationships, and need nothing extra.

    Full PDF/A conformance - embedded fonts, colour spaces, transparency - is a
    far larger rule set, and veraPDF is the implementation the industry treats as
    authoritative. veraPDF runs on Java, so this script fetches both.

    Neither tools\jre nor tools\verapdf is committed to the repository: together
    they are roughly 200 MB, and this script reproduces them on demand.

    After running this, set enableVeraPdf to true in App.config to turn the
    conformance pass on.

.PARAMETER Force
    Re-download and reinstall even if the folders already exist.
#>
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$toolsDir    = $PSScriptRoot
$jreDir      = Join-Path $toolsDir 'jre'
$veraPdfDir  = Join-Path $toolsDir 'verapdf'
$downloadDir = Join-Path $toolsDir '_download'

$jreUrl       = 'https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jre/hotspot/normal/eclipse'
$veraPdfUrl   = 'https://software.verapdf.org/releases/verapdf-installer.zip'

if ($Force) {
    foreach ($dir in @($jreDir, $veraPdfDir, $downloadDir)) {
        if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    }
}

New-Item -ItemType Directory -Force -Path $downloadDir | Out-Null

# --- Java runtime ---------------------------------------------------------

if (Test-Path (Join-Path $jreDir 'bin\java.exe')) {
    Write-Host "JRE already present at $jreDir"
}
else {
    $jreZip = Join-Path $downloadDir 'jre.zip'
    Write-Host 'Downloading the Temurin 21 JRE...'
    Invoke-WebRequest -Uri $jreUrl -OutFile $jreZip -UseBasicParsing

    Write-Host 'Extracting the JRE...'
    Expand-Archive -Path $jreZip -DestinationPath $downloadDir -Force

    # The archive unpacks to a versioned folder name; normalise it to tools\jre
    # so App.config and the application do not have to track the version.
    $extracted = Get-ChildItem -Path $downloadDir -Directory |
                 Where-Object { $_.Name -like 'jdk-*' } |
                 Select-Object -First 1

    if (-not $extracted) { throw 'The JRE archive did not contain the expected folder.' }

    Move-Item -Path $extracted.FullName -Destination $jreDir
}

$javaExe = Join-Path $jreDir 'bin\java.exe'
& $javaExe -version 2>&1 | Write-Host

# --- veraPDF --------------------------------------------------------------

if (Test-Path (Join-Path $veraPdfDir 'bin')) {
    Write-Host "veraPDF already present at $veraPdfDir"
}
else {
    $veraZip = Join-Path $downloadDir 'verapdf-installer.zip'
    Write-Host 'Downloading the veraPDF installer...'
    Invoke-WebRequest -Uri $veraPdfUrl -OutFile $veraZip -UseBasicParsing

    $installerRoot = Join-Path $downloadDir 'verapdf-installer'
    Expand-Archive -Path $veraZip -DestinationPath $installerRoot -Force

    $installerJar = Get-ChildItem -Path $installerRoot -Recurse -Filter '*izpack-installer*.jar' |
                    Select-Object -First 1

    if (-not $installerJar) { throw 'The veraPDF installer jar was not found in the archive.' }

    # veraPDF ships an IzPack installer. It is driven headlessly with an
    # auto-install script; only the CLI pack is needed, the GUI is not.
    $autoInstall = Join-Path $downloadDir 'auto-install.xml'
    @"
<?xml version="1.0" encoding="UTF-8" standalone="no"?>
<AutomatedInstallation langpack="eng">
  <com.izforge.izpack.panels.htmlhello.HTMLHelloPanel id="welcome"/>
  <com.izforge.izpack.panels.target.TargetPanel id="install_dir">
    <installpath>$veraPdfDir</installpath>
  </com.izforge.izpack.panels.target.TargetPanel>
  <com.izforge.izpack.panels.packs.PacksPanel id="sdk_pack_select">
    <pack index="0" name="veraPDF GUI" selected="false"/>
    <pack index="1" name="veraPDF CLI" selected="true"/>
    <pack index="2" name="veraPDF Documentation" selected="false"/>
  </com.izforge.izpack.panels.packs.PacksPanel>
  <com.izforge.izpack.panels.install.InstallPanel id="install"/>
  <com.izforge.izpack.panels.finish.FinishPanel id="finish"/>
</AutomatedInstallation>
"@ | Set-Content -Path $autoInstall -Encoding UTF8

    Write-Host 'Installing veraPDF...'
    & $javaExe -jar $installerJar.FullName $autoInstall | Write-Host

    if (-not (Test-Path (Join-Path $veraPdfDir 'bin'))) {
        throw "veraPDF installation did not produce $veraPdfDir\bin."
    }
}

# --- verify ---------------------------------------------------------------

Write-Host 'Verifying the veraPDF installation...'
& $javaExe -classpath "$veraPdfDir\bin\*" `
    -Dfile.encoding=UTF8 -XX:+IgnoreUnrecognizedVMOptions `
    --add-exports=java.base/sun.security.pkcs=ALL-UNNAMED `
    org.verapdf.apps.GreenfieldCliWrapper --version | Write-Host

Remove-Item -Recurse -Force $downloadDir -ErrorAction SilentlyContinue

Write-Host ''
Write-Host 'Done. Set enableVeraPdf to true in "eDocument Validator\App.config" to enable the PDF/A conformance pass.'
