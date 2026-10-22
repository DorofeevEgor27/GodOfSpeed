$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$unityProject = Join-Path $repoRoot 'GodOfSpeed'
$versionFile = Join-Path $unityProject 'ProjectSettings/ProjectVersion.txt'
$versionMatch = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion: (.+)$'
if (-not $versionMatch) { throw 'Unity version is missing from ProjectVersion.txt.' }
$unityVersion = $versionMatch.Matches[0].Groups[1].Value.Trim()
$unityEditor = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$unityVersion/Editor/Unity.exe"
$gitCommand = Get-Command git -ErrorAction Stop
$manifest = Get-Content -LiteralPath (Join-Path $unityProject 'Packages/manifest.json') -Raw | ConvertFrom-Json
$null = Get-Content -LiteralPath (Join-Path $unityProject 'Packages/packages-lock.json') -Raw | ConvertFrom-Json
if (-not $manifest.dependencies) { throw 'Package manifest has no dependencies.' }
if (-not (Test-Path -LiteralPath $unityEditor)) { throw "Required Unity editor not found: $unityEditor" }
[pscustomobject]@{
    Project = $unityProject
    UnityVersion = $unityVersion
    UnityEditor = $unityEditor
    Git = $gitCommand.Source
    PackageJson = 'Valid'
    UnityProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue).Count
    Verification = 'Environment only; compilation and Play Mode were not run.'
}
