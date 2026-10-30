# Semi-automatic Suno session (ADR-0011): opens the prompt cards with copy buttons and watches the download folder.
# Generate and download on suno.com yourself; every finished download named after a cue goes through music_inbox.
#   .\tools\music-inbox\Start-SunoSession.ps1                       # watches your Downloads folder
#   .\tools\music-inbox\Start-SunoSession.ps1 -Downloads D:\Suno    # or any folder Chrome saves to
param(
    [string]$Downloads = (Join-Path $env:USERPROFILE "Downloads"),
    [string]$AudioRoot = "D:\BuildArtifacts\Codex\hd2d-jrpg\audio"
)
$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$tool = Join-Path $repo "tools\music-inbox"
$sheet = Join-Path $repo "design\audio\cue-sheet.csv"
$ledger = Join-Path $repo "design\audio\LICENSE_LEDGER.csv"
$out = Join-Path $repo "Assets\_Project\Resources\Audio\Music"
$inbox = Join-Path $AudioRoot "_inbox"
$page = Join-Path $AudioRoot "suno-cards.html"
New-Item -ItemType Directory -Force -Path $inbox | Out-Null

python (Join-Path $tool "card_page.py") --cards (Join-Path $repo "design\audio\suno-cards") --ledger $ledger --out $page
Start-Process $page
python (Join-Path $tool "watch_downloads.py") --downloads $Downloads --inbox $inbox --cue-sheet $sheet `
    --out $out --work $AudioRoot --ledger $ledger
