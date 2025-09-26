using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace ScreenshotFlash
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            SetProcessDPIAware(); // disabilita scaling DPI, cattura precisa

            bool isPartial = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

            Rectangle bounds;
            if (isPartial)
            {
                Application.EnableVisualStyles();
                using (var selector = new SelectionForm())
                {
                    if (selector.ShowDialog() != DialogResult.OK)
                        return;
                    bounds = selector.SelectedRegion;
                }
            }
            else
            {
                Screen activeScreen = Screen.FromPoint(Cursor.Position);
                bounds = activeScreen.Bounds;

                Thread flashThread = new Thread(() =>
                {
                    Form flash = new Form
                    {
                        StartPosition = FormStartPosition.Manual,
                        Location = bounds.Location,
                        Size = bounds.Size,
                        BackColor = Color.Cyan,
                        FormBorderStyle = FormBorderStyle.None,
                        TopMost = true,
                        ShowInTaskbar = false,
                        Opacity = 0.0
                    };

                    Region borderRegion = new Region(new Rectangle(0, 0, bounds.Width, bounds.Height));
                    borderRegion.Exclude(new Rectangle(30, 30, bounds.Width - 60, bounds.Height - 60));
                    flash.Region = borderRegion;

                    flash.BackColor = Color.Cyan;
                    flash.TransparencyKey = Color.Black;

                    flash.Shown += (s, e) =>
                    {
                        for (double op = 0.0; op <= 0.8; op += 0.1)
                        {
                            flash.Opacity = op;
                            flash.Refresh();
                            Application.DoEvents();
                            Thread.Sleep(20);
                        }

                        Application.DoEvents();
                        Thread.Sleep(150);

                        for (double op = 0.8; op >= 0.0; op -= 0.1)
                        {
                            flash.Opacity = op;
                            flash.Refresh();
                            Application.DoEvents();
                            Thread.Sleep(20);
                        }

                        flash.Close();
                    };

                    Application.Run(flash);
                });

                flashThread.SetApartmentState(ApartmentState.STA);
                flashThread.Start();
                flashThread.Join();
            }

            using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(
                        bounds.Left,
                        bounds.Top,
                        0,
                        0,
                        new Size(bounds.Width, bounds.Height),
                        CopyPixelOperation.SourceCopy);
                }

                Clipboard.SetImage(bmp); // Copia negli appunti

                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Screenshots"
                );

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                // Cerca il numero piÃ¹ alto tra i file esistenti e genera un nome univoco
                int counter = 1;
                foreach (string file in Directory.GetFiles(folder, "screen_*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!name.StartsWith("screen_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string remainder = name.Substring("screen_".Length);
                    int separatorIndex = remainder.IndexOf('_');
                    string numericPart = separatorIndex >= 0 ? remainder[..separatorIndex] : remainder;

                    if (int.TryParse(numericPart, out int existing) && existing >= counter)
                    {
                        counter = existing + 1;
                    }
                }

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string filename = $"screen_{counter}_{timestamp}.png";
                string filePath = Path.Combine(folder, filename);

                while (File.Exists(filePath))
                {
                    counter++;
                    filename = $"screen_{counter}_{timestamp}.png";
                    filePath = Path.Combine(folder, filename);
                }

                bmp.Save(filePath, ImageFormat.Png);
            }
        }
    }
}
