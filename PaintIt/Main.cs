using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PaintIt
{
    public partial class Main : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        private const int _LeftDown = 0x02;
        private const int _LeftUp = 0x04;

        private Point _lastPoint = new Point(-1, -1);
        private uint _pid;

        public Main()
        {
            InitializeComponent();
        }

        private void LeftMouseClick(int xpos1, int ypos1, int xpos2, int ypos2)
        {
            SetCursorPos(xpos1, ypos1);
            mouse_event(_LeftDown, xpos1, ypos1, 0, 0);
            SetCursorPos(xpos2, ypos2);
            mouse_event(_LeftUp, xpos2, ypos2, 0, 0);

            _lastPoint = new Point(xpos2, ypos2);
        }

        private Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private uint GetActiveProcess()
        {
            uint pid;
            GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            return pid;
        }

        private void Draw(Bitmap image, int x, int y)
        {
            int x1, y1, x2, y2;
            for (int i = 0; i < image.Height; i++)
            {
                int c = 0;
                for (int j = 0; j < image.Width; j++)
                {
                    Color oc = image.GetPixel(j, i);


                    uint xx = GetActiveProcess();
                    if ((_lastPoint.X != -1 && !_lastPoint.Equals(MousePosition)) || _pid != xx)
                    {
                        return;
                    }

                    if ((oc.R * 0.3) + (oc.G * 0.59) + (oc.B * 0.11) < 110)
                    {
                        if (j == image.Width - 1 && c > 0)
                        {
                            x1 = x + j;
                            y1 = y + i;

                            LeftMouseClick(x1 - c, y1, x1, y1);
                            c = 0;
                            Thread.Sleep(10);
                        }
                        else
                        {
                            c++;
                        }
                    }
                    else if (c > 0)
                    {
                        x2 = x + j - 1;
                        y2 = y + i - 1;

                        LeftMouseClick(x2 - c, y2, x2, y2);
                        c = 0;
                        Thread.Sleep(10);
                    }
                }
            }
        }

        private void btnDraw_Click(object sender, EventArgs e)
        {
            if (pbxPreview.Image == null)
            {
                MessageBox.Show("No image selected. Click 'Browse' to select an image.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                Hide();
                Thread.Sleep(500);

                Bitmap image = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);

                using (Graphics g = Graphics.FromImage(image))
                {
                    g.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, image.Size, CopyPixelOperation.SourceCopy);
                    g.DrawRectangle(Pens.Red, 1, 1, image.Width - 3, image.Height - 3);
                }

                ScreenOverlay overlay = new ScreenOverlay(image);
                Show();

                if (overlay.ShowDialog() == DialogResult.OK)
                {
                    WindowState = FormWindowState.Minimized;
                    image.Dispose();
                    Bitmap bmp = ResizeImage(pbxPreview.Image, overlay.Dim.Width, overlay.Dim.Height);
                    Thread.Sleep(1000);

                    _lastPoint = new Point(-1, -1);
                    _pid = GetActiveProcess();

                    bmp = MakeGrayScale(bmp);
       
                    bmp = DitherImage(bmp);
                    pbxPreview.Image = bmp;
                    Draw(bmp, overlay.Pos.X, overlay.Pos.Y);
                    WindowState = FormWindowState.Normal;
                }
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = "Open";
                    dlg.Filter = "Image files(*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";

                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        using (Image im = Image.FromFile(dlg.FileName))
                        {
                            pbxPreview.Image = new Bitmap(im);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private Bitmap MakeGrayScale(Bitmap originalBmp)
        {
            Bitmap grayScaleBmp = new Bitmap(originalBmp.Width, originalBmp.Height);

            using (Graphics g = Graphics.FromImage(grayScaleBmp))
            {
                ColorMatrix colorMatrix = new ColorMatrix(
                    new float[][]
                    {
                        new float[] {.3f, .3f, .3f, 0, 0},
                        new float[] {.59f, .59f, .59f, 0, 0},
                        new float[] {.11f, .11f, .11f, 0, 0},
                        new float[] {0, 0, 0, 1, 0},
                        new float[] {0, 0, 0, 0, 1}
                    });


                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);

                    g.DrawImage(originalBmp, new Rectangle(0, 0, originalBmp.Width, originalBmp.Height),
                                0, 0, originalBmp.Width, originalBmp.Height, GraphicsUnit.Pixel, attributes);
                }
            }

            return grayScaleBmp;
        }


        private Bitmap DitherImage(Bitmap bmp)
        {
            int colourBit = 16;
            float errCorrection1 = 7.0f;
            float errCorrection2 = 5.0f;
            float errCorrection3 = 3.0f;


            Color Pix;
            int[,] r1 = new int[bmp.Width, bmp.Height];
            int[,] r2 = new int[bmp.Width, bmp.Height];
            int[,] g1 = new int[bmp.Width, bmp.Height];
            int[,] g2 = new int[bmp.Width, bmp.Height];
            int[,] b1 = new int[bmp.Width, bmp.Height];
            int[,] b2 = new int[bmp.Width, bmp.Height];
            
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Pix = bmp.GetPixel(x, y);
                    r1[x, y] = (Pix.R + Pix.G + Pix.B);
                    g1[x, y] = (Pix.R + Pix.G + Pix.B);
                    b1[x, y] = (Pix.R + Pix.G + Pix.B);
                }
            }

            int err_r;
            int err_g;
            int err_b;

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    if (g1[x, y] < 128)
                    {
                        g2[x, y] = 0;
                    }else
                    {
                        g2[x, y] = 255;
                    }

                    if (r1[x, y] < 128)
                    {
                        r2[x, y] = 0;
                    }else
                    {
                        r2[x, y] = 255;
                    }

                    if (b1[x, y] < 128)
                    {
                        b2[x, y] = 0;
                    }else
                    {
                        b2[x, y] = 255;
                    }

                    err_r = r1[x, y] - r2[x, y];
                    err_g = g1[x, y] - g2[x, y];
                    err_b = b1[x, y] - b2[x, y];
                    
                    if (x < bmp.Width - 1) { 
                        r2[x + 1, y] += (int)(err_r * errCorrection1) / colourBit;
                        g2[x + 1, y] += (int)(err_g * errCorrection1) / colourBit;
                        b2[x + 1, y] += (int)(err_b * errCorrection1) / colourBit;
                    }

                    if (y < bmp.Height - 1)
                    {
                        r2[x, y + 1] += (int)(err_r * errCorrection2) / colourBit;
                        g2[x, y + 1] += (int)(err_g * errCorrection2) / colourBit;
                        b2[x, y + 1] += (int)(err_b * errCorrection2) / colourBit;
                    }

                    if (x < bmp.Width - 1 && y < bmp.Height - 1)
                    {
                        r2[x + 1, y + 1] += err_r / colourBit;
                        g2[x + 1, y + 1] += err_g / colourBit;
                        b2[x + 1, y + 1] += err_b / colourBit;
                    }

                    if (x > 0 && y < bmp.Height - 1)
                    {
                        r2[x - 1, y + 1] += (int)(err_r * errCorrection3) / colourBit;
                        g2[x - 1, y + 1] += (int)(err_g * errCorrection3) / colourBit;
                        b2[x - 1, y + 1] += (int)(err_b * errCorrection3) / colourBit;
                    }
                    
                }
            }

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    bmp.SetPixel(x, y, Color.FromArgb(r2[x, y], g2[x, y], b2[x, y]));
                }
            }
            return bmp;
        }
    }
}
