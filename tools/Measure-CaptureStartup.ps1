param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,
    [int]$Runs = 5,
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw 'Executable not found.'
}
if ($Runs -lt 1 -or $TimeoutSeconds -lt 1) {
    throw 'Runs and TimeoutSeconds must be positive.'
}

$Executable = (Resolve-Path -LiteralPath $Executable).Path
$folder = Join-Path ([Environment]::GetFolderPath('MyPictures')) 'Screenshots'
$results = @()

Write-Host 'Use a neutral test desktop. This script does not open, read, or upload screenshots.'
for ($run = 1; $run -le $Runs; $run++) {
    # Use the process start time and a fresh list of filenames to avoid
    # confusing previous screenshots with the current run.
    $before = @{}
    if (Test-Path -LiteralPath $folder) {
        Get-ChildItem -LiteralPath $folder -Filter 'screen_*.png' -File |
            ForEach-Object { $before[$_.Name] = $true }
    }

    $clock = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process -FilePath $Executable -PassThru
    try {
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch {}
            throw "Run $run timed out."
        }
        $clock.Stop()
        if ($process.ExitCode -ne 0) {
            Write-Warning "Run $run exited with code $($process.ExitCode)."
        }

        $created = @()
        if (Test-Path -LiteralPath $folder) {
            $created = @(Get-ChildItem -LiteralPath $folder -Filter 'screen_*.png' -File |
                Where-Object { -not $before.ContainsKey($_.Name) })
        }

        $results += [pscustomobject]@{
            Run = $run
            ElapsedMs = [math]::Round($clock.Elapsed.TotalMilliseconds, 1)
            NewPngCount = $created.Count
        }
    }
    finally {
        $process.Dispose()
    }
}
$results | Format-Table -AutoSize
if ($results.Count -gt 1) {
    $later = @($results | Select-Object -Skip 1 | ForEach-Object { $_.ElapsedMs } |
        Sort-Object)
    $middle = [int][math]::Floor($later.Count / 2)
    $median = if ($later.Count % 2) {
        $later[$middle]
    } else {
        ($later[$middle - 1] + $later[$middle]) / 2
    }
    Write-Host ("First run: {0} ms; later-run median: {1} ms" -f $results[0].ElapsedMs, $median)
}
