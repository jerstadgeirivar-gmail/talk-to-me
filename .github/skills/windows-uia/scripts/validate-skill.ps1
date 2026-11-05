# Usage: ./validate-skill.ps1 (validates the bundle without launching apps or contacting paid services)
[CmdletBinding()]
param([string]$SkillDirectory = (Join-Path $PSScriptRoot '..'))
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path $SkillDirectory).Path
$skill = Get-Content (Join-Path $root 'SKILL.md') -Raw
if ($skill -notmatch '(?s)\A---\r?\n(.*?)\r?\n---') { throw 'Missing YAML frontmatter.' }
$frontmatter = $Matches[1]
if ($frontmatter -notmatch '(?m)^name: ([a-z0-9-]+)\s*$' -or $Matches[1] -ne (Split-Path $root -Leaf)) { throw 'Skill name must match its folder.' }
if ($frontmatter -notmatch '(?m)^description: "([^"\r\n]+)"\s*$') { throw 'Use a quoted discovery description.' }
$description = $Matches[1]
if ($description.Length -gt 1024 -or $description -notlike '*Use for:*' -or $description -notlike '*WHEN:*') { throw 'Discovery description is missing triggers or exceeds 1024 characters.' }
if ($frontmatter -notmatch '(?m)^argument-hint:') { throw 'Missing argument hint.' }
if (($skill -split '\r?\n').Count -ge 500) { throw 'Move detailed content into references.' }
foreach ($required in @('references\principles.md', 'references\checklist.md', 'scripts\invoke-uia.ps1', 'scripts\invoke-talk-to-me.ps1', 'assets\journey-template.json')) {
    if (-not (Test-Path (Join-Path $root $required))) { throw "Missing skill resource: $required" }
}
foreach ($document in Get-ChildItem $root -Filter '*.md' -Recurse) {
    foreach ($match in [regex]::Matches((Get-Content $document.FullName -Raw), '\[[^\]]+\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Split('#')[0]
        if ($target -and $target -notmatch '^https?://') {
            if (-not (Test-Path (Join-Path $document.DirectoryName $target))) { throw "Broken resource link in $($document.Name): $target" }
            if ($document.Name -eq 'SKILL.md' -and -not $target.StartsWith('./')) { throw 'SKILL.md resource links must begin with ./.' }
        }
    }
}
foreach ($script in Get-ChildItem (Join-Path $root 'scripts') -Filter '*.ps1' -Recurse) {
    $tokens = $null; $parseErrors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count -gt 0) { throw "PowerShell syntax errors in $($script.Name): $parseErrors" }
}
foreach ($asset in Get-ChildItem (Join-Path $root 'assets') -Filter '*.json') { [void](Get-Content $asset.FullName -Raw | ConvertFrom-Json) }
'PASS skill discovery metadata, relative links, resources, PowerShell syntax and JSON assets'