<#
.SYNOPSIS
  Runs everything CI runs, locally, in CI order, and prints a summary table (core-instructions/18, 11 and 16).
  The same steps, options, table and exit codes as quality.sh, for machines with PowerShell and no Git Bash.
.DESCRIPTION
  Steps: docs (check-docs.sh --base origin/main) · commits (subject format) · handbook (docs-site.sh build) ·
  format (dotnet format --verify-no-changes) · build (Release) · tests (fast suite) · integration (needs Docker) ·
  doctools (DocTools check) · unity (needs UNITY_EDITOR).
  The docs and handbook steps run bash scripts: without bash on the PATH (Git for Windows installs it) they are SKIPPED, like a
  step that cannot run here for another reason (no Docker, no Node.js, UNITY_EDITOR unset). A skip is not a pass: the summary
  names it, so say in your report what was not run. Output is never hidden.
  Usage: pwsh scripts/dev/quality.ps1 [-Only a,b] [-Skip a,b] [-Base <ref>] [-List]
  Exit code: 0 every step that ran passed; 1 a step failed; 2 the setup or an option is wrong.
#>
param(
    [string[]]$Only = @(),
    [string[]]$Skip = @(),
    [string]$Base = 'origin/main',
    [switch]$List
)
$ErrorActionPreference = 'Continue'  # native commands report through exit codes; the script reads $LASTEXITCODE itself

$allSteps = @('docs', 'commits', 'handbook', 'format', 'build', 'tests', 'integration', 'doctools', 'unity')

# Reports a setup problem or a wrong option and ends the script with exit code 2.
function Stop-Setup([string]$Message) {
    [Console]::Error.WriteLine("quality: $Message")
    exit 2
}

if ($List) { $allSteps; exit 0 }
foreach ($name in @($Only) + @($Skip)) {
    if ($name -and $allSteps -notcontains $name) {
        Stop-Setup "unknown step '$name' (steps: $($allSteps -join ' '))"
    }
}

$root = git rev-parse --show-toplevel 2>$null
if (-not $root) { Stop-Setup 'run this inside the repository.' }
Set-Location -LiteralPath $root

$script:rows = @()  # one object per step: Step, Result (PASS | FAIL | SKIP), Sec, Note

# True when the step is selected by -Only / -Skip.
function Test-Selected([string]$Name) {
    if ($Only.Count -gt 0 -and $Only -notcontains $Name) { return $false }
    return $Skip -notcontains $Name
}

# Adds the outcome of a step to the summary: name, PASS|FAIL|SKIP, seconds, note.
function Add-Row([string]$Name, [string]$Result, [int]$Seconds, [string]$Note) {
    $script:rows += [pscustomobject]@{ Step = $Name; Result = $Result; Sec = $Seconds; Note = $Note }
}

# Runs one step: the exit code of the last native command in the script block is the step's result. Its output goes to the terminal.
function Invoke-Step([string]$Name, [string]$Title, [scriptblock]$Action) {
    if (-not (Test-Selected $Name)) { return }
    Write-Host ''
    Write-Host "== [$Name] $Title"
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $global:LASTEXITCODE = 0
    & $Action | Out-Host  # Out-Host: the output is shown as it comes and never captured
    $code = $global:LASTEXITCODE
    $seconds = [int]$watch.Elapsed.TotalSeconds
    if ($code -eq 0) { Add-Row $Name 'PASS' $seconds '' } else { Add-Row $Name 'FAIL' $seconds "exit code $code" }
}

# Records a skipped step with the reason and prints it.
function Skip-Step([string]$Name, [string]$Reason) {
    if (-not (Test-Selected $Name)) { return }
    Write-Host ''
    Write-Host "== [$Name] SKIPPED: $Reason"
    Add-Row $Name 'SKIP' 0 $Reason
}

# The commit subject format of the CI job `docs` (core-instructions/18) for every commit of this branch since $Base.
function Test-CommitSubjects {
    $pattern = '^(feat|fix|test|docs|refactor|perf|build|ci|chore|wip)\((M[0-9]+-[0-9]{2}[a-z]?|CR-[0-9]{4}(-T[0-9]+)?|repo|kit)\)!?: .{3,}$'
    git rev-parse --verify -q $Base *> $null
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("quality: the base '$Base' does not exist here (git fetch origin).")
        $global:LASTEXITCODE = 1
        return
    }
    $bad = 0
    foreach ($subject in (git log --format=%s "$Base..HEAD")) {
        if ($subject -like 'Merge *' -or $subject -like 'Revert *') { continue }
        if ($subject -cnotmatch $pattern) { Write-Host "Bad commit subject: $subject"; $bad = 1 }
    }
    if ($bad -eq 0) { Write-Host 'commit subjects: OK' }
    $global:LASTEXITCODE = $bad
}

$hasBash = [bool](Get-Command bash -ErrorAction SilentlyContinue)
$hasNode = [bool](Get-Command node -ErrorAction SilentlyContinue)

if ($hasBash) {
    Invoke-Step 'docs' 'documentation and process checks' { bash scripts/dev/check-docs.sh --base $Base }
} else {
    Skip-Step 'docs' 'bash is not on the PATH (install Git for Windows); check-docs.sh needs it'
}
Invoke-Step 'commits' "commit subject format against $Base" { Test-CommitSubjects }
if ($hasBash -and $hasNode) {
    Invoke-Step 'handbook' 'the handbook site builds' { bash scripts/dev/docs-site.sh build }
} else {
    Skip-Step 'handbook' 'bash and Node.js 20+ are needed for the handbook build'
}

if ((Test-Path -LiteralPath 'server/CityBuilder.slnx') -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Stop-Setup 'dotnet is not installed; the .NET steps cannot run.'
}
Invoke-Step 'format' 'dotnet format --verify-no-changes' {
    dotnet restore server/CityBuilder.slnx --locked-mode
    if ($LASTEXITCODE -eq 0) { dotnet format server/CityBuilder.slnx --verify-no-changes --no-restore }
}
Invoke-Step 'build' 'dotnet build (Release)' { dotnet build server/CityBuilder.slnx --no-restore -c Release }
Invoke-Step 'tests' 'fast tests' { dotnet test server/CityBuilder.slnx --no-build -c Release --filter 'Category!=Integration' }

docker info *> $null
if ((Get-Command docker -ErrorAction SilentlyContinue) -and $LASTEXITCODE -eq 0) {
    Invoke-Step 'integration' 'integration tests (Docker)' { dotnet test server/CityBuilder.slnx --no-build -c Release --filter 'Category=Integration' }
} else {
    Skip-Step 'integration' 'Docker is not running (start Docker Desktop; docs/testing/README.md)'
}

Invoke-Step 'doctools' 'generated docs are current (DocTools check)' { dotnet run --project tools/DocTools -c Release -- check }

if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR)) {
    Invoke-Step 'unity' 'Unity tests (close the Unity Editor first)' { & (Join-Path $PSScriptRoot 'test-unity.ps1') }
} else {
    Skip-Step 'unity' 'UNITY_EDITOR is not set (client/README.md > Run the tests)'
}

Write-Host ''
Write-Host '== Quality summary'
if ($script:rows.Count -eq 0) { Stop-Setup 'no step was selected.' }
$script:rows | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
$failed = @($script:rows | Where-Object Result -eq 'FAIL').Count
$skipped = @($script:rows | Where-Object Result -eq 'SKIP').Count
if ($failed -ne 0) {
    [Console]::Error.WriteLine("FAILED: $failed step(s) did not pass.")
    exit 1
}
if ($skipped -ne 0) {
    Write-Host "OK, but $skipped step(s) were skipped: that is not what CI runs. Say so in your report."
} else {
    Write-Host 'OK: every step passed.'
}
