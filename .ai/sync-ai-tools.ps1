# sync-ai-tools.ps1 -- Windows-friendly equivalent of sync-ai-tools.sh
#
# Copies .ai/skills and .ai/rules into .claude/ and .github/.
# On Windows, copies (not symlinks) are used since symlinks require
# Developer Mode or admin rights.
#
# Targets:
#   .claude/skills/<name>                       <- .ai/skills/<name>
#   .claude/rules/<name>.md                     <- .ai/rules/<name>.md
#   .github/skills/<name>                       <- .ai/skills/<name>
#   .github/instructions/<name>.instructions.md <- .ai/rules/<name>.md
#
# Usage:
#   pwsh .ai/sync-ai-tools.ps1
#   (or run the sync-ai-tools.cmd wrapper to bypass PowerShell execution policy)

$ErrorActionPreference = 'Stop'

$RepoRoot           = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$AiSkills           = Join-Path $RepoRoot '.ai/skills'
$AiRules            = Join-Path $RepoRoot '.ai/rules'
$ClaudeSkills       = Join-Path $RepoRoot '.claude/skills'
$ClaudeRules        = Join-Path $RepoRoot '.claude/rules'
$GithubSkills       = Join-Path $RepoRoot '.github/skills'
$GithubInstructions = Join-Path $RepoRoot '.github/instructions'
$VsCodeSettingsDir  = Join-Path $RepoRoot '.vscode'
$VsCodeSettingsFile = Join-Path $VsCodeSettingsDir 'settings.json'

# Remove copies in $TargetDir whose name corresponds to an entry in $SourceDir.
function Remove-ManagedCopies {
    param([string]$TargetDir, [string]$SourceDir)
    if (-not (Test-Path $TargetDir)) { return }
    Get-ChildItem -Path $TargetDir -Force | ForEach-Object {
        $name  = $_.Name
        $check = $name -replace '\.instructions\.md$', ''
        $found = $false
        if (Test-Path (Join-Path $SourceDir $name))  { $found = $true }
        elseif (Test-Path (Join-Path $SourceDir $check)) { $found = $true }
        else {
            foreach ($ext in 'md', 'mdc') {
                if (Test-Path (Join-Path $SourceDir "$check.$ext")) { $found = $true; break }
            }
        }
        if ($found) { Remove-Item -Recurse -Force $_.FullName }
    }
}

# Extract globs array from frontmatter. Returns array of glob strings.
function Get-Globs {
    param([string]$FilePath)
    $n = 0
    $globs = @()
    foreach ($line in (Get-Content $FilePath -Encoding UTF8)) {
        if ($line -eq '---') {
            $n++
            if ($n -eq 1) { continue }
            break
        }
        if ($n -eq 1 -and $line -match '^globs:\s*(.+)') {
            $val = $matches[1].Trim() -replace '[\[\]]', ''
            $globs = $val -split ',' |
                ForEach-Object { $_.Trim().Trim('"') } |
                Where-Object { $_ -ne '' }
        }
    }
    return $globs
}

# Returns $true when frontmatter contains: globs: []
function Test-HasEmptyGlobsArray {
    param([string]$FilePath)
    $n = 0
    foreach ($line in (Get-Content $FilePath -Encoding UTF8)) {
        if ($line -eq '---') {
            $n++
            if ($n -eq 1) { continue }
            break
        }
        if ($n -eq 1 -and $line -match '^globs:\s*\[\s*\]\s*$') {
            return $true
        }
    }
    return $false
}

# Return file body — everything after the closing --- of the frontmatter.
function Get-Body {
    param([string]$FilePath)
    $lines = Get-Content $FilePath -Encoding UTF8
    $n = 0
    $result = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $lines) {
        if ($line -eq '---') {
            $n++
            if ($n -eq 1) { continue }
            if ($n -eq 2) { continue }
        }
        if ($n -eq 0 -or $n -ge 2) { [void]$result.Add($line) }
    }
    return $result.ToArray()
}

# Write line-based content as UTF-8 without BOM so frontmatter parsers read '---' at byte 0.
function Write-Utf8NoBomLines {
    param([string]$Path, [string[]]$Lines)
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllLines($Path, $Lines, $utf8NoBom)
}

# Write a Claude rule file: globs -> paths:
function Write-ClaudeRule {
    param([string]$Src, [string]$Dst)
    $globs = Get-Globs $Src
    $body  = Get-Body $Src
    $content = [System.Collections.Generic.List[string]]::new()
    if ($globs.Count -gt 0) {
        [void]$content.Add('---')
        [void]$content.Add('paths:')
        foreach ($g in $globs) { [void]$content.Add("  - `"$g`"") }
        [void]$content.Add('---')
    } elseif (Test-HasEmptyGlobsArray $Src) {
        [void]$content.Add('---')
        [void]$content.Add('paths:')
        [void]$content.Add('  - "**"')
        [void]$content.Add('---')
    }
    foreach ($line in $body) { [void]$content.Add($line) }
    Write-Utf8NoBomLines -Path $Dst -Lines $content.ToArray()
}

# Write a Copilot instructions file: globs -> applyTo:
function Write-CopilotRule {
    param([string]$Src, [string]$Dst)
    $globs = Get-Globs $Src
    $body  = @(Get-Body $Src | ForEach-Object {
        $_ -replace '\]\((\./)?([\w-]+)\.md([#)])', ']($1$2.instructions.md$3'
    })
    $content = [System.Collections.Generic.List[string]]::new()
    if ($globs.Count -gt 0) {
        [void]$content.Add('---')
        [void]$content.Add("applyTo: `"$($globs -join ',')`"")
        [void]$content.Add('---')
    } elseif (Test-HasEmptyGlobsArray $Src) {
        [void]$content.Add('---')
        [void]$content.Add('applyTo: "**"')
        [void]$content.Add('---')
    }
    foreach ($line in $body) { [void]$content.Add($line) }
    Write-Utf8NoBomLines -Path $Dst -Lines $content.ToArray()
}

function Ensure-VsCodeSettings {
    $isVsCode = ($env:TERM_PROGRAM -eq 'vscode') -or (-not [string]::IsNullOrWhiteSpace($env:VSCODE_PID))
    if (-not $isVsCode) { return }

    New-Item -ItemType Directory -Force -Path $VsCodeSettingsDir | Out-Null

    # Merge, never overwrite: keep existing keys (e.g. WASM debug config) and
    # comments intact; only add the chat.* keys when they are missing.
    $existing = ''
    if (Test-Path $VsCodeSettingsFile) {
        $existing = [System.IO.File]::ReadAllText($VsCodeSettingsFile)
    }
    if ($existing -match 'chat\.instructionsFilesLocations' -and
        $existing -match 'chat\.agentSkillsLocations') { return }

    if ([string]::IsNullOrWhiteSpace($existing)) {
        $existing = "{`r`n}`r`n"
    }

    $block = @'
    "chat.instructionsFilesLocations": {
        ".claude/rules": false,
        "~/.claude/rules": false
    },
    "chat.agentSkillsLocations": {
        "~/.claude/skills": false,
        ".claude/skills": false
    }
'@

    $close = $existing.LastIndexOf('}')
    if ($close -lt 0) { return }
    $head = $existing.Substring(0, $close).TrimEnd()
    if (-not ($head.EndsWith(',') -or $head.EndsWith('{'))) {
        $head += ','
    }

    $merged = $head + "`r`n" + $block + "`r`n}`r`n"
    Write-Utf8NoBomLines -Path $VsCodeSettingsFile -Lines ($merged -split "`r?`n")
    Write-Host "  vscode: settings.json updated (merged)"
}

Write-Host "Syncing .ai/ skills and rules..."
Write-Host ""

# Skills: copies (unchanged)
Remove-ManagedCopies $ClaudeSkills $AiSkills
Remove-ManagedCopies $GithubSkills $AiSkills

# Rules: always written files — clear existing before rewriting
Remove-ManagedCopies $ClaudeRules $AiRules
Remove-ManagedCopies $GithubInstructions $AiRules
foreach ($dir in $ClaudeRules, $GithubInstructions) {
    if (Test-Path $dir) {
        Get-ChildItem -Path $dir -Filter '*.md' | Remove-Item -Force
    }
}

New-Item -ItemType Directory -Force -Path $ClaudeSkills, $ClaudeRules, $GithubSkills, $GithubInstructions | Out-Null

$changed = 0

if (Test-Path $AiSkills) {
    Get-ChildItem -Path $AiSkills -Directory | ForEach-Object {
        $name = $_.Name
        Copy-Item -Recurse -Force $_.FullName (Join-Path $ClaudeSkills $name)
        Copy-Item -Recurse -Force $_.FullName (Join-Path $GithubSkills $name)
        Write-Host "  skill: $name"
        $changed += 2
    }
}

if (Test-Path $AiRules) {
    Get-ChildItem -Path $AiRules -File | ForEach-Object {
        $name = $_.Name
        $base = [System.IO.Path]::GetFileNameWithoutExtension($name)
        Write-ClaudeRule  $_.FullName (Join-Path $ClaudeRules $name)
        Write-CopilotRule $_.FullName (Join-Path $GithubInstructions "$base.instructions.md")
        Write-Host "  rule:  $name"
        $changed += 2
    }
}

$ruleReferences = Join-Path $AiRules 'references'
if (Test-Path $ruleReferences) {
    Copy-Item -Recurse -Force $ruleReferences (Join-Path $ClaudeRules 'references')
    Copy-Item -Recurse -Force $ruleReferences (Join-Path $GithubInstructions 'references')
    $changed += 2
}

Ensure-VsCodeSettings

Write-Host ""
if ($changed -gt 0) {
    Write-Host "Done. $changed items synced."
} else {
    Write-Host "Done. No skills or rules found in .ai/."
}
