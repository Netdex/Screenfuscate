using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Screenfuscate
{
    public partial class Screenfuscate : Form
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private LowLevelKeyboardProc _proc;
        private static IntPtr _hookID = IntPtr.Zero;

        private readonly Random _rnd = new Random();
        private readonly OpenFileDialog fileDialog = new OpenFileDialog()
        {
            Filter = "Image Files (*.bmp, *.jpg, *.gif, *.png)|*.bmp;*.jpg;*.gif;*.png"
        };

        private bool _invisible = false;
        private bool _resizing = false;
        private bool _distort = false;
        private bool _transparent = true;
        private Point _resizePoint = new Point(0, 0);
        private int _regionWidth = 150;
        private int _regionHeight = 150;
        private Image image;
        private Rectangle lastRegion = new Rectangle(0, 0, 0, 0);

        public Screenfuscate()
        {
            InitializeComponent();
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        private void Screenfuscate_Load(object sender, EventArgs e)
        {
            // Prevent flickering
            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.UserPaint, true);

            SetBounds(0, 0, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            Graphics g = args.Graphics;
            // Make graphics draw fast
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.None;

            // Draw a image if it exists
            Point cursor = Cursor.Position;
            if (image != null)
            {
                g.DrawImageUnscaled(image, 0, 0);
            }
            if (_resizing)
            {
                Rectangle region = new Rectangle(_resizePoint.X, _resizePoint.Y, cursor.X - _resizePoint.X,
                    cursor.Y - _resizePoint.Y);

                g.FillRectangle(Brushes.Purple, region);
                g.DrawRectangle(
                    new Pen(
                        new HatchBrush(HatchStyle.DiagonalCross, Color.Red, Color.Yellow), 20), region);
            }
            else
            {
                // Draw the invisible region, the whole screen if the invisible key is being pressed
                if (_invisible)
                    g.FillRectangle(Brushes.Purple, 0, 0, Width, Height);
                else
                {
                    Rectangle region = new Rectangle(cursor.X - _regionWidth/2, cursor.Y - _regionHeight/2, _regionWidth,
                        _regionHeight);
                    if (_transparent)
                    {
                        g.FillRectangle(new HatchBrush(HatchStyle.Percent50, this.TransparencyKey, Color.Transparent),
                            region);
                        g.FillRectangle(Brushes.Purple, cursor.X - 5, cursor.Y - 5, 10, 10);
                    }
                    else
                    {
                        g.FillRectangle(Brushes.Purple, region);
                    }
                }
            }
            if (_distort)
            {
                string text = "Distorting";
                Font font = new Font("Arial", 20, FontStyle.Bold);
                g.DrawString(text,
                    font, Brushes.Black, Width - g.MeasureString(text, font).Width, 0);
            }
        }

        private void selectImageToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // Allow user to select a custom image
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (image != null)
                        image.Dispose();
                    image = Resample(Image.FromFile(fileDialog.FileName),
                        Screen.PrimaryScreen.Bounds.Size);
                    Invalidate();
                }
                catch
                {
                    MessageBox.Show("Could not read image!", "Image Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void screencapToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // Allow user to set the image to a screencap of the current screen
            _invisible = true;
            Invalidate();
            new Thread((ThreadStart)delegate
            {
                Thread.Sleep(100);
                image = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppPArgb);
                using (Graphics g = Graphics.FromImage(image))
                {
                    g.CopyFromScreen(Screen.PrimaryScreen.Bounds.X,
                             Screen.PrimaryScreen.Bounds.Y,
                             0, 0,
                             image.Size,
                             CopyPixelOperation.SourceCopy);
                }
                _invisible = false;
                Invoke((MethodInvoker)Invalidate);
            }).Start();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    switch (key)
                    {
                        case Keys.LMenu:
                            if (!_resizing)
                            {
                                _resizePoint = Cursor.Position;
                                _resizing = true;
                                Invalidate();
                            }
                            break;
                        case Keys.F4:
                            screenfuscateToolStripMenuItem.ShowDropDown();
                            break;
                        case Keys.F3:
                            _distort = !_distort;
                            Invalidate();
                            break;
                        case Keys.F6:
                            _transparent = !_transparent;
                            break;
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    switch (key)
                    {
                        case Keys.LMenu:
                            _resizing = false;
                            Point cursor = Cursor.Position;
                            int newWidth = cursor.X - _resizePoint.X;
                            int newHeight = cursor.Y - _resizePoint.Y;
                            if (newWidth > 0 && newHeight > 0)
                            {
                                _regionWidth = newWidth;
                                _regionHeight = newHeight;
                            }
                            Invalidate();
                            break;
                    }
                }

            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            Point cursor = Cursor.Position;
            Rectangle region;
            if (_resizing)
            {
                // Refresh the entire window since I'm lazy
                region = new Rectangle(0, 0, Width, Height);
            }
            else
            {
                // Create a region only around the viewing area to reduce CPU usage
                region = new Rectangle(
                    cursor.X - _regionWidth / 2,
                    cursor.Y - _regionHeight / 2,
                    _regionWidth,
                    _regionHeight);
            }
            // Invalidate previous and current regions to avoid creating artifacts
            Invalidate(region);
            Invalidate(lastRegion);
            lastRegion = region;

            // Distort the image for the lolz
            if (_distort && image != null)
            {
                Rectangle srcRegion = GetRandomRegion(Width, Height, 3);
                Rectangle destRegion = GetRandomRegion(Width, Height, srcRegion.Width, srcRegion.Height);
                using (Image srcImage = BitmapCopy(image, srcRegion))
                {
                    using (Image destImage = BitmapCopy(image, destRegion))
                    {
                        using (Graphics g = Graphics.FromImage(image))
                        {
                            g.DrawImageUnscaled(srcImage, destRegion);
                            g.DrawImageUnscaled(destImage, srcRegion);
                        }
                    }
                }
                Invalidate(srcRegion);
                Invalidate(destRegion);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutBox().ShowDialog();
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private void Screenfuscate_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
        }

        private Rectangle GetRandomRegion(int maxwidth, int maxheight, int w, int h)
        {
            int x = _rnd.Next(maxwidth - w);
            int y = _rnd.Next(maxheight - h);
            Rectangle distRegion = new Rectangle(x, y, w, h);
            return distRegion;
        }
        private Rectangle GetRandomRegion(int width, int height, int divvy)
        {
            int w = _rnd.Next(width) / divvy + 1;
            int h = _rnd.Next(height) / divvy + 1;
            int x = _rnd.Next(width - w);
            int y = _rnd.Next(height - h);
            Rectangle distRegion = new Rectangle(x, y, w, h);
            return distRegion;
        }

        private static Bitmap Resample(Image img, Size size)
        {
            var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            using (var gr = Graphics.FromImage(bmp))
                gr.DrawImage(img, new Rectangle(Point.Empty, size));
            return bmp;
        }

        static public Image BitmapCopy(Image srcBitmap, Rectangle section)
        {
            Bitmap bmp = new Bitmap(section.Width, section.Height);
            Graphics g = Graphics.FromImage(bmp);
            g.DrawImage(srcBitmap, 0, 0, section, GraphicsUnit.Pixel);
            g.Dispose();
            return bmp;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
