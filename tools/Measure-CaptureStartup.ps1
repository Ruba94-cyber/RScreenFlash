[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,
    [ValidateRange(1, 100)]
    [int]$Runs = 5,
    [ValidateRange(0, 100)]
    [int]$ShiftRuns = 5,
    [ValidateRange(0, 100)]
    [int]$CancelRuns = 0,
    [ValidateRange(1, 300)]
    [int]$TimeoutSeconds = 20,
    [string]$CsvPath
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) {
    throw "Executable does not exist: $Executable"
}
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$processName = [IO.Path]::GetFileNameWithoutExtension($Executable)
if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
    throw 'Close every running RScreenFlash instance before starting this benchmark.'
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
if (-not ('RScreenFlashBench.Native' -as [type])) {
    $references = @([System.Windows.Forms.Form].Assembly.Location, [System.Drawing.Bitmap].Assembly.Location)
    Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RScreenFlashBench
{
    public sealed class SyntheticBackdrop : IDisposable
    {
        private readonly ManualResetEventSlim ready = new ManualResetEventSlim(false);
        private readonly Thread thread;
        private Form form;
        private Exception startupError;

        public SyntheticBackdrop()
        {
            thread = new Thread(Show) { IsBackground = true, Name = "SyntheticCaptureBenchmarkBackdrop" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!ready.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("Synthetic desktop did not start.");
            if (startupError != null)
                throw new InvalidOperationException("Synthetic desktop failed to start.", startupError);
        }

        private void Show()
        {
            try
            {
                Rectangle bounds = SystemInformation.VirtualScreen;
                form = new Form
                {
                    FormBorderStyle = FormBorderStyle.None,
                    StartPosition = FormStartPosition.Manual,
                    Bounds = bounds,
                    BackColor = Color.FromArgb(20, 42, 74),
                    TopMost = true,
                    ShowInTaskbar = false
                };
                form.Paint += (sender, args) =>
                {
                    Rectangle area = form.ClientRectangle;
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        area, Color.FromArgb(25, 65, 110), Color.FromArgb(18, 30, 55), 35f))
                        args.Graphics.FillRectangle(brush, area);
                    using (Pen pen = new Pen(Color.FromArgb(90, 130, 180), 2f))
                    {
                        for (int x = 0; x < area.Width; x += 64)
                            args.Graphics.DrawLine(pen, x, 0, x, area.Height);
                        for (int y = 0; y < area.Height; y += 64)
                            args.Graphics.DrawLine(pen, 0, y, area.Width, y);
                    }
                    using (Font font = new Font("Segoe UI", 26f, FontStyle.Bold))
                    using (Brush textBrush = new SolidBrush(Color.White))
                        args.Graphics.DrawString("SYNTHETIC CAPTURE TEST", font, textBrush, 32f, 32f);
                };
                form.Shown += (sender, args) => ready.Set();
                Application.Run(form);
            }
            catch (Exception ex)
            {
                startupError = ex;
                ready.Set();
            }
        }

        public void Dispose()
        {
            try
            {
                if (form != null)
                    form.BeginInvoke(new Action(() => form.Close()));
            }
            catch { }
            thread.Join(TimeSpan.FromSeconds(3));
            ready.Dispose();
        }
    }

    public static class Native
    {
        private const uint KeyUp = 0x0002;
        private const uint LeftDown = 0x0002;
        private const uint LeftUp = 0x0004;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool EnumWindowsCallback(IntPtr hwnd, IntPtr state);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr state);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);
        [DllImport("shcore.dll", SetLastError = true)]
        private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extraInfo);
        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")]
        public static extern bool IsClipboardFormatAvailable(uint format);

        public static bool GetDpiScaleAt(int x, int y, out double scaleX, out double scaleY)
        {
            scaleX = 1.0;
            scaleY = 1.0;
            IntPtr previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
            if (previous == IntPtr.Zero)
                return false;
            try
            {
                IntPtr monitor = MonitorFromPoint(new NativePoint { X = x, Y = y }, 2);
                if (monitor == IntPtr.Zero)
                    return false;
                uint dpiX, dpiY;
                if (GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0 || dpiX == 0 || dpiY == 0)
                    return false;
                scaleX = dpiX / 96.0;
                scaleY = dpiY / 96.0;
                return true;
            }
            finally
            {
                SetThreadDpiAwarenessContext(previous);
            }
        }

        public static void ShiftDown() { keybd_event(0x10, 0, 0, UIntPtr.Zero); }
        public static void ShiftUp() { keybd_event(0x10, 0, KeyUp, UIntPtr.Zero); }
        public static void EscapeDown() { keybd_event(0x1B, 0, 0, UIntPtr.Zero); }
        public static void EscapeUp() { keybd_event(0x1B, 0, KeyUp, UIntPtr.Zero); }
        public static bool MoveCursor(int x, int y) { return SetCursorPos(x, y); }

        public static bool WaitForVisibleWindow(int processId, int timeoutMilliseconds)
        {
            Stopwatch clock = Stopwatch.StartNew();
            do
            {
                bool found = false;
                EnumWindows(delegate(IntPtr hwnd, IntPtr state)
                {
                    int ownerProcessId;
                    GetWindowThreadProcessId(hwnd, out ownerProcessId);
                    if (ownerProcessId == processId && IsWindowVisible(hwnd))
                    {
                        found = true;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
                if (found)
                    return true;
                Thread.Sleep(10);
            }
            while (clock.ElapsedMilliseconds < timeoutMilliseconds);
            return false;
        }

        public static void Drag(int startX, int startY, int endX, int endY)
        {
            if (!SetCursorPos(startX, startY))
                throw new InvalidOperationException("Cannot position the synthetic selection input.");
            Thread.Sleep(40);
            mouse_event(LeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            if (!SetCursorPos(endX, endY))
                throw new InvalidOperationException("Cannot move the synthetic selection input.");
            Thread.Sleep(40);
            mouse_event(LeftUp, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(40);
        }
    }
}
'@
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactParent = Join-Path $repoRoot '.benchmark-output'
$benchmarkRoot = Join-Path $artifactParent ('capture-' + [guid]::NewGuid().ToString('N'))
$resolvedArtifactParent = [IO.Path]::GetFullPath($artifactParent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$resolvedBenchmarkRoot = [IO.Path]::GetFullPath($benchmarkRoot)
if (-not $resolvedBenchmarkRoot.StartsWith($resolvedArtifactParent, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to use a benchmark directory outside this repository.'
}
New-Item -ItemType Directory -Path $benchmarkRoot -Force | Out-Null

$oldOutputDirectory = $env:RSCREENFLASH_BENCHMARK_OUTPUT_DIRECTORY
$oldTimingsPath = $env:RSCREENFLASH_BENCHMARK_TIMINGS
$results = New-Object System.Collections.Generic.List[object]
$profiles = @(
    [pscustomobject]@{ Name = 'OddLandscape'; Width = 671; Height = 377 },
    [pscustomobject]@{ Name = 'Portrait'; Width = 277; Height = 661 },
    [pscustomobject]@{ Name = 'Panoramic'; Width = 1001; Height = 311 },
    [pscustomobject]@{ Name = 'Classic4x3'; Width = 800; Height = 600 }
)
$primary = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$scaleX = 1.0
$scaleY = 1.0
if (-not [RScreenFlashBench.Native]::GetDpiScaleAt($primary.X, $primary.Y, [ref]$scaleX, [ref]$scaleY)) {
    throw 'Cannot determine the primary display DPI for dimension validation.'
}
$backdrop = $null

function Get-NullableValue($object, [string]$property) {
    if ($null -eq $object) { return $null }
    return $object.$property
}

function Invoke-CaptureRun([string]$mode, [int]$runNumber, $profile) {
    $runFolder = Join-Path $benchmarkRoot ('{0}-{1:D2}' -f $mode, $runNumber)
    $captureFolder = Join-Path $runFolder 'captures'
    New-Item -ItemType Directory -Path $captureFolder -Force | Out-Null
    $timingPath = Join-Path $runFolder 'timings.csv'
    $expectedWidth = [int][math]::Round($primary.Width * $scaleX)
    $expectedHeight = [int][math]::Round($primary.Height * $scaleY)
    $selectionLabel = ''

    if ($mode -eq 'Shift') {
        $profile = $profiles[($runNumber - 1) % $profiles.Count]
        if ($profile.Width -gt ($primary.Width - 40) -or $profile.Height -gt ($primary.Height - 40)) {
            throw "Display $($primary.Width)x$($primary.Height) cannot fit the $($profile.Name) selection profile."
        }
        $startX = $primary.X + 20
        $startY = $primary.Y + 20
        $endX = $startX + $profile.Width
        $endY = $startY + $profile.Height
        $expectedWidth = [int][math]::Round($profile.Width * $scaleX)
        $expectedHeight = [int][math]::Round($profile.Height * $scaleY)
        $selectionLabel = $profile.Name
    }

    if (-not [RScreenFlashBench.Native]::MoveCursor(
        $primary.X + [int]($primary.Width / 2),
        $primary.Y + [int]($primary.Height / 2))) {
        throw 'Cannot position the cursor on the synthetic primary display.'
    }

    $clipboardSequenceBefore = [RScreenFlashBench.Native]::GetClipboardSequenceNumber()
    $processClock = [Diagnostics.Stopwatch]::StartNew()
    $process = $null
    $shiftDown = $false
    $timeToCapture = $null
    $timeToSavedPng = $null
    $timeToClipboard = $null
    $png = $null
    try {
        if ($mode -eq 'Shift') {
            [RScreenFlashBench.Native]::ShiftDown()
            $shiftDown = $true
        }

        $env:RSCREENFLASH_BENCHMARK_OUTPUT_DIRECTORY = $captureFolder
        $env:RSCREENFLASH_BENCHMARK_TIMINGS = $timingPath
        $process = Start-Process -FilePath $Executable -PassThru
        $env:RSCREENFLASH_BENCHMARK_OUTPUT_DIRECTORY = $oldOutputDirectory
        $env:RSCREENFLASH_BENCHMARK_TIMINGS = $oldTimingsPath

        if ($mode -eq 'Shift') {
            if (-not [RScreenFlashBench.Native]::WaitForVisibleWindow($process.Id, 8000)) {
                throw 'Shift selection overlay did not become visible.'
            }
            [RScreenFlashBench.Native]::ShiftUp()
            $shiftDown = $false
            [RScreenFlashBench.Native]::Drag($startX, $startY, $endX, $endY)
        }

        while (-not $process.HasExited) {
            if ($null -eq $timeToCapture) {
                $newPngs = @(Get-ChildItem -LiteralPath $captureFolder -Filter 'screen_*.png' -File -ErrorAction SilentlyContinue)
                if ($newPngs.Count -eq 1) {
                    $png = $newPngs[0]
                    $timeToCapture = [math]::Round($processClock.Elapsed.TotalMilliseconds, 3)
                }
            }

            if ($null -ne $png -and $null -eq $timeToSavedPng) {
                try {
                    $stream = [IO.File]::Open($png.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
                    try {
                        if ($stream.Length -gt 8) {
                            $timeToSavedPng = [math]::Round($processClock.Elapsed.TotalMilliseconds, 3)
                        }
                    }
                    finally { $stream.Dispose() }
                }
                catch [IO.IOException] { }
            }

            if ($null -eq $timeToClipboard -and
                [RScreenFlashBench.Native]::GetClipboardSequenceNumber() -ne $clipboardSequenceBefore -and
                [RScreenFlashBench.Native]::IsClipboardFormatAvailable(8)) {
                $timeToClipboard = [math]::Round($processClock.Elapsed.TotalMilliseconds, 3)
            }

            if ($processClock.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                try { $process.Kill() } catch { }
                if (-not $process.WaitForExit(3000)) { throw 'Timed out and could not stop the capture process.' }
                throw "Run $mode/$runNumber timed out."
            }
            Start-Sleep -Milliseconds 2
        }

        $process.WaitForExit()
        $processClock.Stop()
        $processExitElapsedMs = [math]::Round($processClock.Elapsed.TotalMilliseconds, 3)
        $exitCode = $process.ExitCode
        $newPngs = @(Get-ChildItem -LiteralPath $captureFolder -Filter 'screen_*.png' -File -ErrorAction SilentlyContinue)
        if ($newPngs.Count -ne 1) { throw "Run $mode/$runNumber created $($newPngs.Count) PNG files; expected exactly one." }
        $png = $newPngs[0]
        if ($exitCode -ne 0) { throw "Run $mode/$runNumber exited with code $exitCode." }
        # A fast process can create, close and exit between polling intervals.
        # In that case process exit is the best observable upper bound for these
        # completion times, and the file/clipboard checks below still validate them.
        if ($null -eq $timeToCapture) { $timeToCapture = $processExitElapsedMs }
        if ($null -eq $timeToSavedPng) {
            $stream = [IO.File]::Open($png.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
            try {
                if ($stream.Length -le 8) { throw "Run $mode/$runNumber left an incomplete PNG." }
                $timeToSavedPng = $processExitElapsedMs
            }
            finally { $stream.Dispose() }
        }
        if ($null -eq $timeToClipboard -and
            [RScreenFlashBench.Native]::GetClipboardSequenceNumber() -ne $clipboardSequenceBefore -and
            [RScreenFlashBench.Native]::IsClipboardFormatAvailable(8)) {
            $timeToClipboard = $processExitElapsedMs
        }
        if ($null -eq $timeToCapture) { throw "Run $mode/$runNumber never created its PNG." }
        if ($null -eq $timeToSavedPng) { throw "Run $mode/$runNumber never completed and unlocked its PNG." }

        $image = [System.Drawing.Image]::FromFile($png.FullName)
        try {
            $actualWidth = $image.Width
            $actualHeight = $image.Height
        }
        finally { $image.Dispose() }
        if ($actualWidth -ne $expectedWidth -or $actualHeight -ne $expectedHeight) {
            throw "Run $mode/$runNumber produced ${actualWidth}x${actualHeight}; expected ${expectedWidth}x${expectedHeight}."
        }

        $timing = $null
        if (Test-Path -LiteralPath $timingPath -PathType Leaf) {
            $timing = Import-Csv -LiteralPath $timingPath | Select-Object -First 1
        }
        $clipboardAvailable = [RScreenFlashBench.Native]::IsClipboardFormatAvailable(8)
        if ($null -eq $timeToClipboard -or -not $clipboardAvailable) {
            throw "Run $mode/$runNumber did not make image data available in the clipboard."
        }

        $results.Add([pscustomobject]@{
            Mode = $mode
            Profile = $selectionLabel
            Run = $runNumber
            Launch = if ($runNumber -eq 1) { 'First process launch; OS cache not flushed' } else { 'Subsequent process launch' }
            ProcessStartToCaptureMs = $timeToCapture
            ProcessStartToSavedPngMs = $timeToSavedPng
            ProcessStartToClipboardMs = $timeToClipboard
            CaptureDurationMs = Get-NullableValue $timing 'CaptureDurationMs'
            MainToCaptureMs = Get-NullableValue $timing 'TimeToCaptureMs'
            PngWriteDurationMs = Get-NullableValue $timing 'PngWriteDurationMs'
            MainToSaveMs = Get-NullableValue $timing 'TimeToSaveMs'
            ClipboardDurationMs = Get-NullableValue $timing 'ClipboardDurationMs'
            MainToClipboardMs = Get-NullableValue $timing 'TimeToClipboardMs'
            MainDurationMs = Get-NullableValue $timing 'MainDurationMs'
            ClipboardAvailable = $clipboardAvailable
            Width = $actualWidth
            Height = $actualHeight
            ExitCode = $exitCode
            TotalProcessMs = $processExitElapsedMs
        })
    }
    finally {
        if ($shiftDown) { [RScreenFlashBench.Native]::ShiftUp() }
        $env:RSCREENFLASH_BENCHMARK_OUTPUT_DIRECTORY = $oldOutputDirectory
        $env:RSCREENFLASH_BENCHMARK_TIMINGS = $oldTimingsPath
        if ($null -ne $process) { $process.Dispose() }
        $processClock.Stop()
    }
}

function Invoke-CancelRun([int]$runNumber) {
    $runFolder = Join-Path $benchmarkRoot ('Cancel-{0:D2}' -f $runNumber)
    $captureFolder = Join-Path $runFolder 'captures'
    New-Item -ItemType Directory -Path $captureFolder -Force | Out-Null
    $clipboardSequenceBefore = [RScreenFlashBench.Native]::GetClipboardSequenceNumber()
    $process = $null
    $shiftDown = $false
    $escapeDown = $false
    try {
        $env:RSCREENFLASH_BENCHMARK_OUTPUT_DIRECTORY = $captureFolder
        $env:RSCREENFLASH_BENCHMARK_TIMINGS = Join-Path $runFolder 'timings.csv'
        [RScreenFlashBench.Native]::ShiftDown()
        $shiftDown = $true
        $process = Start-Process -FilePath $Executable -PassThru
        $env:RSCREENFLASH_BENCHMARK_OUTPUT_DIRECTORY = $oldOutputDirectory
        $env:RSCREENFLASH_BENCHMARK_TIMINGS = $oldTimingsPath

        if (-not [RScreenFlashBench.Native]::WaitForVisibleWindow($process.Id, 8000)) {
            throw "Cancel/$runNumber selection overlay did not become visible."
        }
        [RScreenFlashBench.Native]::ShiftUp()
        $shiftDown = $false
        [RScreenFlashBench.Native]::EscapeDown()
        $escapeDown = $true
        Start-Sleep -Milliseconds 40
        [RScreenFlashBench.Native]::EscapeUp()
        $escapeDown = $false

        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch { }
            if (-not $process.WaitForExit(3000)) { throw 'Timed out and could not stop the canceled capture process.' }
            throw "Cancel/$runNumber timed out."
        }
        if ($process.ExitCode -ne 0) { throw "Cancel/$runNumber exited with code $($process.ExitCode)." }
        $pngs = @(Get-ChildItem -LiteralPath $captureFolder -Filter 'screen_*.png' -File -ErrorAction SilentlyContinue)
        if ($pngs.Count -ne 0) { throw "Cancel/$runNumber created $($pngs.Count) PNG files; expected none." }
        if ([RScreenFlashBench.Native]::GetClipboardSequenceNumber() -ne $clipboardSequenceBefore) {
            throw "Cancel/$runNumber changed the clipboard."
        }
        Write-Host ("Cancel/{0}: Escape closed selection; no PNG, clipboard change, or residual process." -f $runNumber)
    }
    finally {
        if ($shiftDown) { [RScreenFlashBench.Native]::ShiftUp() }
        if ($escapeDown) { [RScreenFlashBench.Native]::EscapeUp() }
        $env:RSCREENFLASH_BENCHMARK_OUTPUT_DIRECTORY = $oldOutputDirectory
        $env:RSCREENFLASH_BENCHMARK_TIMINGS = $oldTimingsPath
        if ($null -ne $process) { $process.Dispose() }
    }
}

try {
    $backdrop = New-Object RScreenFlashBench.SyntheticBackdrop
    Write-Host 'Synthetic desktop active. Captures and timing data are confined to this run and removed afterward.'
    for ($i = 1; $i -le $Runs; $i++) { Invoke-CaptureRun 'Standard' $i $null }
    for ($i = 1; $i -le $ShiftRuns; $i++) { Invoke-CaptureRun 'Shift' $i $null }
    for ($i = 1; $i -le $CancelRuns; $i++) { Invoke-CancelRun $i }

    $results | Format-Table Mode,Profile,Run,ProcessStartToCaptureMs,ProcessStartToSavedPngMs,ProcessStartToClipboardMs,TotalProcessMs,Width,Height -AutoSize
    foreach ($group in ($results | Group-Object Mode,Profile)) {
        $ordered = @($group.Group | Sort-Object Run)
        $later = @($ordered | Where-Object Run -gt 1)
        if ($later.Count -gt 0) {
            $totalMedian = ($later | Measure-Object TotalProcessMs -Average).Average
            $captureMedian = ($later | Measure-Object ProcessStartToCaptureMs -Average).Average
            $saveMedian = ($later | Measure-Object ProcessStartToSavedPngMs -Average).Average
            $clipboardMedian = ($later | Measure-Object ProcessStartToClipboardMs -Average).Average
            Write-Host ("{0}: first total {1} ms; later means total/capture/save/clipboard {2}/{3}/{4}/{5} ms" -f
                $group.Name,
                $ordered[0].TotalProcessMs,
                [math]::Round($totalMedian, 1),
                [math]::Round($captureMedian, 1),
                [math]::Round($saveMedian, 1),
                [math]::Round($clipboardMedian, 1))
        }
    }

    if ($CsvPath) {
        $csvFullPath = [IO.Path]::GetFullPath($CsvPath)
        $csvParent = Split-Path -Parent $csvFullPath
        if (-not (Test-Path -LiteralPath $csvParent -PathType Container)) {
            New-Item -ItemType Directory -Path $csvParent -Force | Out-Null
        }
        $results | Export-Csv -LiteralPath $csvFullPath -NoTypeInformation -Encoding UTF8
        Write-Host "Numeric benchmark results written to $csvFullPath"
    }
}
finally {
    if ($null -ne $backdrop) { $backdrop.Dispose() }
    $env:RSCREENFLASH_BENCHMARK_OUTPUT_DIRECTORY = $oldOutputDirectory
    $env:RSCREENFLASH_BENCHMARK_TIMINGS = $oldTimingsPath
    if (Test-Path -LiteralPath $benchmarkRoot -PathType Container) {
        $resolved = [IO.Path]::GetFullPath($benchmarkRoot)
        if ($resolved.StartsWith($resolvedArtifactParent, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
