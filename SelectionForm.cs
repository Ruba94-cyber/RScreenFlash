using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ScreenshotFlash
{
    public class SelectionForm : Form
    {
        private Point start;
        private Rectangle selection;
        private Rectangle virtualScreen;

        public Rectangle SelectedRegion => new Rectangle(
            selection.X + virtualScreen.X,
            selection.Y + virtualScreen.Y,
            selection.Width,
            selection.Height);

        public SelectionForm()
        {
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0.20;
            this.BackColor = Color.Black;
            this.TopMost = true;
            this.KeyPreview = true;
            this.Cursor = Cursors.Cross;

            virtualScreen = GetTrueVirtualScreen();
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(virtualScreen.X, virtualScreen.Y);
            this.Size = new Size(virtualScreen.Width, virtualScreen.Height);

            this.Load += (s, e) => this.Focus();
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
            start = e.Location;
            selection = new Rectangle(e.Location, Size.Empty);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
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
            // Sfondo scuro semitrasparente
            using (Brush dark = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
            {
                e.Graphics.FillRectangle(dark, this.ClientRectangle);
            }

            // Area selezionata blu trasparente
            using (Brush cyan = new SolidBrush(Color.FromArgb(100, 0, 255, 255)))
            {
                e.Graphics.FillRectangle(cyan, selection);
            }

            // Bordo cyan
            using (Pen pen = new Pen(Color.Cyan, 2))
            {
                e.Graphics.DrawRectangle(pen, selection);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (selection.Width > 0 && selection.Height > 0)
            {
                this.DialogResult = DialogResult.OK;
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
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
