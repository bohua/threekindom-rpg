param(
    [ValidateSet('Setup','EditMode','PlayMode','Build')][string]$Action = 'Setup',
    [string]$Editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe',
    [string]$RunId = ('run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (!(Test-Path -LiteralPath $Editor)) { throw "Unity Editor not installed: $Editor" }
if ($RunId -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Invalid RunId' }
$disk = Get-Partition -DriveLetter D | Get-Disk
if (!$disk -or (Get-Item D:\).LinkType) { throw 'Physical D drive is required.' }
$output = "D:\BuildArtifacts\Codex\hd2d-jrpg\$RunId"
New-Item -ItemType Directory -Force $output | Out-Null
if ((Get-Item $output).LinkType) { throw 'Artifact directory cannot be a link.' }
$arguments = @('-batchmode', '-projectPath', $project, '-logFile', "$output\$Action.log")
switch ($Action) {
    'Setup' { $arguments += @('-quit', '-executeMethod', 'ProjectSetup.Create') }
    'Build' {
        if (!(Test-Path "$project\Assets\Game\Scenes\Glade.unity")) { throw 'Run Setup first.' }
        $env:JRPG_BUILD_OUTPUT = "$output\Windows\MoonlitGlade.exe"
        $arguments += @('-quit', '-executeMethod', 'ProjectSetup.BuildWindows')
    }
    default {
        $arguments += @('-runTests', '-testPlatform', $Action, '-testResults', "$output\$Action.xml")
    }
}
# Start-Process requires explicit quotes for Windows paths containing spaces.
$quoted = $arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }
$process = Start-Process -FilePath $Editor -ArgumentList $quoted -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Unity exited $($process.ExitCode); inspect $output\$Action.log" }
if ($Action -eq 'Setup' -and !(Test-Path "$project\Assets\Game\Scenes\Glade.unity")) { throw 'Scene not generated.' }
if ($Action -in @('EditMode','PlayMode')) {
    if (!(Test-Path "$output\$Action.xml")) { throw 'Unity did not produce test results.' }
    [xml]$tests = Get-Content -Raw "$output\$Action.xml"
    if ([int]$tests.'test-run'.total -le 0 -or $tests.'test-run'.result -ne 'Passed') { throw 'Tests failed or none ran.' }
}
if ($Action -eq 'Build' -and !(Test-Path $env:JRPG_BUILD_OUTPUT)) { throw 'Build output missing.' }
Write-Output "Completed $Action. Evidence: $output"
