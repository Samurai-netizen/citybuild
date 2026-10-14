<#
.SYNOPSIS
  Runs the Unity tests of client/CityBuilder headless: EditMode first, then PlayMode (core-instructions/08 and 09).
  The same steps and exit codes as test-unity.sh.
.DESCRIPTION
  Set UNITY_EDITOR to the Unity executable of the project's editor, then run: pwsh scripts/dev/test-unity.ps1
    Windows: $env:UNITY_EDITOR = 'C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe'
    macOS:   $env:UNITY_EDITOR = '/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity'
  The Editor must be closed for this project: a Unity project can be open only once.
  Results (NUnit XML) and Unity's log go to client/CityBuilder/Logs/test-unity/ (git ignores it).
  Exit code: 0 every test passed; 1 a test failed, no test ran, or Unity failed; 2 the setup is wrong (UNITY_EDITOR, project).
#>
$ErrorActionPreference = 'Stop'

function Stop-Setup([string]$Message) {
    [Console]::Error.WriteLine($Message)
    exit 2
}

# Windows PowerShell 5.1 turns a native command's stderr into a terminating error under 'Stop', so git runs with 'Continue'.
$root = & { $ErrorActionPreference = 'Continue'; git rev-parse --show-toplevel 2>$null }
if (-not $root) { Stop-Setup 'test-unity: run this inside the repository.' }
$project = [IO.Path]::GetFullPath((Join-Path $root 'client/CityBuilder'))
$out = Join-Path $project 'Logs/test-unity'

$versionFile = Join-Path $project 'ProjectSettings/ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) { Stop-Setup "test-unity: no Unity project at $project" }
$wanted = (Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value.Trim()

if ([string]::IsNullOrWhiteSpace($env:UNITY_EDITOR)) {
    Stop-Setup ("test-unity: the environment variable UNITY_EDITOR is not set.`n" +
        "  Set it to the Unity executable of editor $wanted, for example (Windows):`n" +
        "  `$env:UNITY_EDITOR = 'C:\Program Files\Unity\Hub\Editor\$wanted\Editor\Unity.exe'")
}
$unity = $env:UNITY_EDITOR
if (Test-Path -LiteralPath $unity -PathType Container) {
    $insideApp = Join-Path $unity 'Contents/MacOS/Unity'  # a macOS Unity.app folder
    if (Test-Path -LiteralPath $insideApp -PathType Leaf) { $unity = $insideApp }
}
if (-not (Test-Path -LiteralPath $unity -PathType Leaf)) {
    Stop-Setup ("test-unity: UNITY_EDITOR is not an executable file: $env:UNITY_EDITOR`n" +
        "  It must point to the Unity program of editor $wanted, not to the Unity Hub.")
}

New-Item -ItemType Directory -Force -Path $out | Out-Null

# The whole number in name="N" of an element, or -1 when it is missing or not a number.
function Get-Count($Element, [string]$Name) {
    $value = 0
    if ($null -ne $Element -and [int]::TryParse($Element.GetAttribute($Name), [ref]$value)) { return $value }
    return -1
}

$script:failedRuns = 0
$script:summary = @()

function Invoke-UnityTests([string]$Platform) {
    $results = Join-Path $out "$Platform.xml"
    $log = Join-Path $out "$Platform.log"
    Remove-Item -LiteralPath $results, $log -Force -ErrorAction SilentlyContinue  # a result file from an earlier run must never be mistaken for this one
    Write-Host "== Unity $Platform tests (log: $log)"

    # Start-Process -Wait: Unity.exe is a GUI program, so a plain call would not wait for it.
    $arguments = @('-batchmode', '-projectPath', "`"$project`"", '-runTests', '-testPlatform', $Platform,
        '-testResults', "`"$results`"", '-logFile', "`"$log`"")
    $code = (Start-Process -FilePath $unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow).ExitCode

    $document = $null
    $run = $null
    if (Test-Path -LiteralPath $results -PathType Leaf) {
        try {
            $document = [xml](Get-Content -LiteralPath $results -Raw)
            $run = $document.DocumentElement
        } catch {
            $run = $null
        }
    }
    $total = Get-Count $run 'total'
    $passed = Get-Count $run 'passed'
    $failed = Get-Count $run 'failed'
    $skipped = Get-Count $run 'skipped'

    if ($null -eq $run -or $run.Name -ne 'test-run' -or ($total, $passed, $failed, $skipped) -contains -1) {
        $line = "${Platform}: FAILED, Unity exit code $code and no readable result file ($results)"
        if ((Test-Path -LiteralPath $log -PathType Leaf) -and (Select-String -LiteralPath $log -Pattern 'another unity instance' -Quiet)) {
            $line += '; close the Unity Editor that has this project open'
        } elseif ((Test-Path -LiteralPath $log -PathType Leaf) -and (Select-String -LiteralPath $log -Pattern 'error CS' -SimpleMatch -CaseSensitive -Quiet)) {
            $errors = Select-String -LiteralPath $log -Pattern 'error CS' -SimpleMatch -CaseSensitive | Select-Object -First 5 | ForEach-Object { $_.Line }
            $line += "; the scripts do not compile:`n" + ($errors -join "`n")
        }
        $script:failedRuns++
    } elseif ($total -eq 0) {
        $line = "${Platform}: FAILED, no test ran (Unity exit code $code)"
        $script:failedRuns++
    } elseif ($failed -gt 0 -or $code -ne 0) {
        $line = "${Platform}: FAILED, passed $passed of $total, failed $failed, skipped $skipped (Unity exit code $code)"
        foreach ($case in $document.SelectNodes("//test-case[@result='Failed']")) {
            $line += "`n    failed: " + $case.GetAttribute('fullname')
        }
        $script:failedRuns++
    } else {
        $line = "${Platform}: passed $passed of $total, skipped $skipped"
    }
    $script:summary += $line
}

$stopwatch = [Diagnostics.Stopwatch]::StartNew()
Invoke-UnityTests 'EditMode'
Invoke-UnityTests 'PlayMode'

Write-Host ''
Write-Host ("== Unity test summary ({0} s)" -f [int]$stopwatch.Elapsed.TotalSeconds)
$script:summary | ForEach-Object { Write-Host $_ }
if ($script:failedRuns -ne 0) {
    [Console]::Error.WriteLine("FAILED: $($script:failedRuns) of 2 runs did not pass. Details: $out")
    exit 1
}
Write-Host 'OK: all Unity tests passed.'
