using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RScreenFlash
{
    static class Program
    {
        private static Stopwatch s_applicationStopwatch;
        private static string s_benchmarkTimingsPath;
        private static bool s_shiftMode;
        private static double s_captureDurationMilliseconds;
        private static double s_timeToCaptureMilliseconds;
        private static double s_pngWriteDurationMilliseconds;
        private static double s_timeToSaveMilliseconds;
        private static double s_clipboardDurationMilliseconds;
        private static double s_timeToClipboardMilliseconds;
        private static bool s_clipboardAvailable;

        private enum PROCESS_DPI_AWARENESS
        {
            PROCESS_DPI_UNAWARE = 0,
            PROCESS_SYSTEM_DPI_AWARE = 1,
            PROCESS_PER_MONITOR_DPI_AWARE = 2
        }

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = new IntPtr(-3);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = new IntPtr(-2);

        private static bool s_isPerMonitorAware;

        internal static bool IsPerMonitorAware => s_isPerMonitorAware;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("shcore.dll", SetLastError = true)]
        private static extern int SetProcessDpiAwareness(PROCESS_DPI_AWARENESS awareness);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        [STAThread]
        static void Main()
        {
            s_applicationStopwatch = Stopwatch.StartNew();
            s_benchmarkTimingsPath = Environment.GetEnvironmentVariable("RSCREENFLASH_BENCHMARK_TIMINGS");
            s_shiftMode = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

            try
            {
                InitializeDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                Rectangle bounds;
                if (s_shiftMode)
                {
                    using (var selector = new SelectionForm())
                    {
                        if (selector.ShowDialog() != DialogResult.OK)
                            return;
                        bounds = selector.SelectedRegion;
                    }
                }
                else
                {
                    bounds = GetMonitorBoundsFromCursor();
                }

                if (CaptureAndSaveScreenshot(bounds))
                {
                    // Save the image before showing feedback. The overlay can
                    // never delay capture or PNG creation or appear in the image.
                    RunFlash(bounds);
                }
            }
            finally
            {
                s_applicationStopwatch.Stop();
                WriteBenchmarkTimings();
            }
        }

        private static bool CaptureAndSaveScreenshot(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                Environment.ExitCode = 1;
                return false;
            }

            Stopwatch captureStopwatch = Stopwatch.StartNew();
            try
            {
                using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;

                        g.CopyFromScreen(
                            bounds.Left,
                            bounds.Top,
                            0,
                            0,
                            bounds.Size,
                            CopyPixelOperation.SourceCopy);
                    }

                    captureStopwatch.Stop();
                    s_captureDurationMilliseconds = captureStopwatch.Elapsed.TotalMilliseconds;
                    s_timeToCaptureMilliseconds = s_applicationStopwatch.Elapsed.TotalMilliseconds;

                    string benchmarkFolder = Environment.GetEnvironmentVariable("RSCREENFLASH_BENCHMARK_OUTPUT_DIRECTORY");
                    string folder = string.IsNullOrWhiteSpace(benchmarkFolder)
                        ? Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                            "Screenshots")
                        : Path.GetFullPath(benchmarkFolder);

                    Stopwatch saveStopwatch = Stopwatch.StartNew();
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    // A timestamp and a random suffix avoid a full directory scan
                    // even when thousands of screenshots already exist.
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
                    // CreateNew atomically prevents concurrent captures from
                    // overwriting one another, without scanning the folder.
                    for (int attempt = 0; ; attempt++)
                    {
                        if (attempt >= 10)
                            throw new IOException("Unable to allocate a unique screenshot filename.");

                        string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
                        string filePath = Path.Combine(folder, $"screen_{timestamp}_{suffix}.png");
                        FileStream stream;
                        try
                        {
                            stream = new FileStream(
                                filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        }
                        catch (IOException)
                        {
                            // Only a filename collision should be retried.
                            // Do not hide permission, disk, or filesystem errors.
                            if (!File.Exists(filePath))
                                throw;
                            continue;
                        }

                        try
                        {
                            using (stream)
                            {
                                Stopwatch pngWriteStopwatch = Stopwatch.StartNew();
                                bmp.Save(stream, ImageFormat.Png);
                                pngWriteStopwatch.Stop();
                                s_pngWriteDurationMilliseconds = pngWriteStopwatch.Elapsed.TotalMilliseconds;
                            }
                        }
                        catch
                        {
                            stream.Dispose();
                            // Remove an incomplete file created by this process.
                            try { File.Delete(filePath); }
                            catch (IOException) { }
                            catch (UnauthorizedAccessException) { }
                            throw;
                        }
                        break;
                    }

                    saveStopwatch.Stop();
                    s_timeToSaveMilliseconds = s_applicationStopwatch.Elapsed.TotalMilliseconds;

                    // Saving is independent of clipboard availability.
                    // Copy only after the PNG has been safely written.
                    Stopwatch clipboardStopwatch = Stopwatch.StartNew();
                    s_clipboardAvailable = TryCopyToClipboard(bmp);
                    clipboardStopwatch.Stop();
                    s_clipboardDurationMilliseconds = clipboardStopwatch.Elapsed.TotalMilliseconds;
                    s_timeToClipboardMilliseconds = s_applicationStopwatch.Elapsed.TotalMilliseconds;
                }

                return true;
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                MessageBox.Show(
                    $"An error occurred while capturing the screen: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                if (captureStopwatch.IsRunning)
                {
                    captureStopwatch.Stop();
                    s_captureDurationMilliseconds = captureStopwatch.Elapsed.TotalMilliseconds;
                }
            }
        }

        private static bool TryCopyToClipboard(Bitmap bitmap)
        {
            // Clipboard contention is transient (e.g. another application owns it).
            // Retry briefly without persisting or transmitting image data.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    Clipboard.SetImage(bitmap);
                    return true;
                }
                catch (ExternalException)
                {
                    if (attempt == 2)
                        return false;

                    Thread.Sleep(25);
                }
            }

            return false;
        }

        private static void WriteBenchmarkTimings()
        {
            if (string.IsNullOrWhiteSpace(s_benchmarkTimingsPath))
                return;

            try
            {
                string path = Path.GetFullPath(s_benchmarkTimingsPath);
                string line = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mode,CaptureDurationMs,TimeToCaptureMs,PngWriteDurationMs,TimeToSaveMs,ClipboardDurationMs,TimeToClipboardMs,MainDurationMs,ClipboardAvailable,ExitCode{0}{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3},{8:F3},{9},{10}{0}",
                    Environment.NewLine,
                    s_shiftMode ? "Shift" : "Standard",
                    s_captureDurationMilliseconds,
                    s_timeToCaptureMilliseconds,
                    s_pngWriteDurationMilliseconds,
                    s_timeToSaveMilliseconds,
                    s_clipboardDurationMilliseconds,
                    s_timeToClipboardMilliseconds,
                    s_applicationStopwatch.Elapsed.TotalMilliseconds,
                    s_clipboardAvailable,
                    Environment.ExitCode);

                byte[] data = Encoding.UTF8.GetBytes(line);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(data, 0, data.Length);
                }
            }
            catch
            {
                // Benchmark diagnostics are opt-in and must never affect capture.
            }
        }

        private static void InitializeDpiAwareness()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return;

            try
            {
                if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                {
                    s_isPerMonitorAware = true;
                    return;
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            try
            {
                if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE))
                {
                    s_isPerMonitorAware = true;
                    return;
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            try
            {
                const int S_OK = 0;
                const int E_ACCESSDENIED = unchecked((int)0x80070005);

                int result = SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);

                if (result == S_OK || result == E_ACCESSDENIED)
                {
                    s_isPerMonitorAware = true;
                    return;
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }

            try
            {
                if (SetProcessDPIAware())
                {
                    s_isPerMonitorAware = false;
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        private static Rectangle GetMonitorBoundsFromCursor()
        {
            if (!GetCursorPos(out POINT pt))
                return GetScaledScreenBounds(Screen.PrimaryScreen);

            IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);

            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);

            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                Rectangle bounds = new Rectangle(
                    monitorInfo.rcMonitor.Left,
                    monitorInfo.rcMonitor.Top,
                    monitorInfo.rcMonitor.Right - monitorInfo.rcMonitor.Left,
                    monitorInfo.rcMonitor.Bottom - monitorInfo.rcMonitor.Top
                );

                return GetDpiScaledBounds(bounds, hMonitor);
            }

            Point cursorPoint = new Point(pt.X, pt.Y);
            Screen activeScreen = Screen.FromPoint(cursorPoint);
            return GetScaledScreenBounds(activeScreen);
        }

        private static void RunFlash(Rectangle bounds)
        {
            // Main is already STA. Reuse its UI thread rather than starting
            // another STA thread and waiting for it to terminate.
            using (Form flash = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Location = bounds.Location,
                Size = bounds.Size,
                BackColor = Color.Cyan,
                FormBorderStyle = FormBorderStyle.None,
                TopMost = true,
                ShowInTaskbar = false,
                Opacity = 0.0
            })
            using (Region borderRegion = new Region(new Rectangle(0, 0, bounds.Width, bounds.Height)))
            {
                if (bounds.Width > 60 && bounds.Height > 60)
                    borderRegion.Exclude(new Rectangle(30, 30, bounds.Width - 60, bounds.Height - 60));

                flash.Region = borderRegion;

                // Keep feedback brief and use the existing UI message loop.
                using (var timer = new System.Windows.Forms.Timer { Interval = 12 })
                {
                    int frame = 0;
                    timer.Tick += (s, e) =>
                    {
                        frame++;
                        if (frame <= 2)
                            flash.Opacity = frame * 0.4;
                        else if (frame == 3)
                            flash.Opacity = 0.8;
                        else if (frame <= 5)
                            flash.Opacity = (6 - frame) * 0.2;
                        else
                        {
                            timer.Stop();
                            flash.Close();
                        }
                    };
                    flash.Shown += (s, e) => timer.Start();
                    flash.FormClosed += (s, e) => timer.Stop();

                    // This runs only after capture, save, and clipboard retries.
                    flash.ShowDialog();
                }
            }
        }

        private static Rectangle GetDpiScaledBounds(Rectangle bounds, IntPtr hMonitor)
        {
            if (IsPerMonitorAware)
                return bounds;

            try
            {
                if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
                {
                    double scaleX = dpiX / 96.0;
                    double scaleY = dpiY / 96.0;

                    return new Rectangle(
                        bounds.X,
                        bounds.Y,
                        (int)Math.Round(bounds.Width * scaleX),
                        (int)Math.Round(bounds.Height * scaleY)
                    );
                }
            }
            catch
            {
            }

            return bounds;
        }

        private static Rectangle GetScaledScreenBounds(Screen screen)
        {
            Rectangle bounds = screen.Bounds;

            if (IsPerMonitorAware)
                return bounds;

            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                float dpiX = g.DpiX;
                float dpiY = g.DpiY;

                if (dpiX != 96.0f || dpiY != 96.0f)
                {
                    double scaleX = dpiX / 96.0;
                    double scaleY = dpiY / 96.0;

                    return new Rectangle(
                        bounds.X,
                        bounds.Y,
                        (int)Math.Round(bounds.Width * scaleX),
                        (int)Math.Round(bounds.Height * scaleY)
                    );
                }
            }

            return bounds;
        }
    }
}
