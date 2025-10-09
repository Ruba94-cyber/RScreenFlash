using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RScreenFlash
{
    public class SelectionForm : Form
    {
        private Point start;
        private Rectangle selection;
        private Rectangle virtualScreen;
        private bool isSelecting = false;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(Point pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;

        public Rectangle SelectedRegion => GetDpiAdjustedSelection();

        public SelectionForm()
        {
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0.25;
            this.BackColor = Color.Black;
            this.TopMost = true;
            this.KeyPreview = true;
            this.Cursor = Cursors.Cross;
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Maximized;

            virtualScreen = GetTrueVirtualScreen();
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(virtualScreen.X, virtualScreen.Y);
            this.Size = new Size(virtualScreen.Width, virtualScreen.Height);

            this.Load += (s, e) =>
            {
                this.Focus();
                this.BringToFront();
                this.Activate();
            };
        }

        private Rectangle GetTrueVirtualScreen()
        {
            int left = Screen.AllScreens.Min(s => s.Bounds.Left);
            int top = Screen.AllScreens.Min(s => s.Bounds.Top);
            int right = Screen.AllScreens.Max(s => s.Bounds.Right);
            int bottom = Screen.AllScreens.Max(s => s.Bounds.Bottom);

            return new Rectangle(left, top, right - left, bottom - top);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                start = e.Location;
                selection = new Rectangle(e.Location, Size.Empty);
                isSelecting = true;
                this.Capture = true;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (isSelecting && e.Button == MouseButtons.Left)
            {
                selection = new Rectangle(
                    Math.Min(start.X, e.X),
                    Math.Min(start.Y, e.Y),
                    Math.Abs(start.X - e.X),
                    Math.Abs(start.Y - e.Y));
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

            // Sfondo scuro semitrasparente
            using (Brush dark = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                e.Graphics.FillRectangle(dark, this.ClientRectangle);
            }

            if (selection.Width > 0 && selection.Height > 0)
            {
                // Area selezionata trasparente (rimuove l'overlay scuro)
                using (Brush clear = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
                {
                    e.Graphics.FillRectangle(clear, selection);
                }

                // Bordo cyan più visibile
                using (Pen pen = new Pen(Color.Cyan, 3))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
                    e.Graphics.DrawRectangle(pen, selection);
                }

                // Mostra dimensioni
                string dimensions = $"{selection.Width} × {selection.Height}";
                using (Font font = new Font("Segoe UI", 12, FontStyle.Bold))
                using (Brush textBrush = new SolidBrush(Color.White))
                using (Brush backgroundBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                {
                    SizeF textSize = e.Graphics.MeasureString(dimensions, font);
                    Point textPos = new Point(
                        selection.X + selection.Width - (int)textSize.Width - 10,
                        selection.Y - (int)textSize.Height - 5
                    );

                    if (textPos.Y < 0) textPos.Y = selection.Y + 5;
                    if (textPos.X < 0) textPos.X = selection.X + 5;

                    Rectangle textRect = new Rectangle(textPos, Size.Ceiling(textSize));
                    textRect.Inflate(5, 2);

                    e.Graphics.FillRectangle(backgroundBrush, textRect);
                    e.Graphics.DrawString(dimensions, font, textBrush, textPos);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && isSelecting)
            {
                this.Capture = false;
                isSelecting = false;

                if (selection.Width > 5 && selection.Height > 5) // Minimo 5x5 pixel
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    // Selezione troppo piccola, resetta
                    selection = Rectangle.Empty;
                    Invalidate();
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return true;
            }
            else if (keyData == Keys.Enter && selection.Width > 5 && selection.Height > 5)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private Rectangle GetDpiAdjustedSelection()
        {
            if (selection.IsEmpty)
                return Rectangle.Empty;

            // Coordinate reali nel virtual screen
            Rectangle realSelection = new Rectangle(
                selection.X + virtualScreen.X,
                selection.Y + virtualScreen.Y,
                selection.Width,
                selection.Height);

            // Trova il monitor per questa selezione
            Point center = new Point(
                realSelection.X + realSelection.Width / 2,
                realSelection.Y + realSelection.Height / 2);

            IntPtr hMonitor = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);

            try
            {
                if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
                {
                    double scaleX = dpiX / 96.0;
                    double scaleY = dpiY / 96.0;

                    // Applica scaling DPI
                    return new Rectangle(
                        (int)Math.Round(realSelection.X * scaleX),
                        (int)Math.Round(realSelection.Y * scaleY),
                        (int)Math.Round(realSelection.Width * scaleX),
                        (int)Math.Round(realSelection.Height * scaleY)
                    );
                }
            }
            catch
            {
                // Fallback senza scaling
            }

            return realSelection;
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (value)
            {
                this.WindowState = FormWindowState.Maximized;
            }
        }
    }
}
