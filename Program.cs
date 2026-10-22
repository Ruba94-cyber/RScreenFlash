using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace RScreenFlash
{
    static class Program
    {
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
            InitializeDpiAwareness();

            bool isPartial = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

            Rectangle bounds;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (isPartial)
            {
                using (var selector = new SelectionForm())
                {
                    if (selector.ShowDialog() != DialogResult.OK)
                        return;
                    bounds = selector.SelectedRegion;
                }

                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    RunFlash(bounds);
                }
            }
            else
            {
                bounds = GetMonitorBoundsFromCursor();
                RunFlash(bounds);
            }

            CaptureAndSaveScreenshot(bounds);
        }

        private static void CaptureAndSaveScreenshot(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

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

                    // A busy clipboard must not prevent saving the screenshot.
                    TryCopyToClipboard(bmp);

                    string folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                        "Screenshots");

                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    // A timestamp and a random suffix avoid a full directory scan
                    // even when thousands of screenshots already exist.
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
                    string filePath;
                    do
                    {
                        string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
                        filePath = Path.Combine(folder, $"screen_{timestamp}_{suffix}.png");
                    }
                    while (File.Exists(filePath));

                    bmp.Save(filePath, ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred while capturing the screen: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void TryCopyToClipboard(Bitmap bitmap)
        {
            // Clipboard contention is transient (e.g. another application owns it).
            // Retry briefly without persisting or transmitting image data.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    Clipboard.SetImage(bitmap);
                    return;
                }
                catch (ExternalException)
                {
                    if (attempt == 2)
                        return;

                    Thread.Sleep(25);
                }
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
            Thread flashThread = new Thread(() =>
            {
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
                {
                    using (Region borderRegion = new Region(new Rectangle(0, 0, bounds.Width, bounds.Height)))
                    {
                        if (bounds.Width > 60 && bounds.Height > 60)
                        {
                            borderRegion.Exclude(new Rectangle(30, 30, bounds.Width - 60, bounds.Height - 60));
                        }

                        flash.Region = borderRegion;
                        flash.BackColor = Color.Cyan;
                        flash.TransparencyKey = Color.Black;

                        flash.Shown += (s, e) =>
                        {
                            for (int step = 1; step <= 4; step++)
                            {
                                flash.Opacity = step * 0.2;
                                flash.Refresh();
                                Application.DoEvents();
                                Thread.Sleep(12);
                            }

                            Application.DoEvents();
                            Thread.Sleep(35);

                            for (int step = 3; step >= 0; step--)
                            {
                                flash.Opacity = step * 0.2;
                                flash.Refresh();
                                Application.DoEvents();
                                Thread.Sleep(12);
                            }

                            flash.Close();
                        };

                        Application.Run(flash);
                    }
                }
            })
            {
                IsBackground = true
            };

            flashThread.SetApartmentState(ApartmentState.STA);
            flashThread.Start();
            flashThread.Join();
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
