using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FlyMenu
{
    /// <summary>
    /// Non-activating, single-row icon bar shown above the running-programs (app) menu.
    /// Left-click launches the pinned .lnk via ShellExecute (so the launched process
    /// inherits the shortcut's AppUserModelID and is grouped correctly under the
    /// original taskbar pin). Right-click opens a small helper menu.
    ///
    /// Window is WS_EX_NOACTIVATE + ShowWithoutActivation + WS_EX_TOOLWINDOW so from
    /// Windows' perspective it never becomes the "current" application and clicking
    /// it does not steal focus from the app the user is working with.
    /// </summary>
    internal sealed class PinnedBarWindow : Form
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TOPMOST    = 0x00000008;

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE    = 0x0003;

        // SetWindowPos flags/handles for reliable, non-activating topmost show
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private const int SW_HIDE          = 0;
        private const int SW_SHOWNOACTIVATE = 4;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE     = 0x0001;
        private const uint SWP_NOMOVE     = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        // SHGetFileInfo for reliable .lnk icon extraction (Icon.ExtractAssociatedIcon
        // returns black/broken icons for many modern shortcuts).
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]  public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern uint ExtractIconEx(string szFileName, int nIconIndex,
            IntPtr[]? phiconLarge, IntPtr[]? phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("user32.dll")]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const uint SHGFI_ICON      = 0x000000100;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_SMALLICON = 0x000000001;

        private readonly ToolStrip strip;
        private readonly List<Icon> ownedIcons = new List<Icon>();
        private readonly List<Bitmap> ownedBitmaps = new List<Bitmap>();

        public PinnedBarWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;

            strip = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                AutoSize = false,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow,
                RenderMode = ToolStripRenderMode.System,
                Padding = new Padding(2),
                ImageScalingSize = new Size(32, 32),
            };
            Controls.Add(strip);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE;
                return;
            }
            base.WndProc(ref m);
        }

        private static Color ParseColor(string? value, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            try
            {
                var s = value.Trim();
                if (s.StartsWith("#"))
                {
                    var hex = s.Substring(1);
                    if (hex.Length == 6)
                        return Color.FromArgb(
                            Convert.ToInt32(hex.Substring(0, 2), 16),
                            Convert.ToInt32(hex.Substring(2, 2), 16),
                            Convert.ToInt32(hex.Substring(4, 2), 16));
                    if (hex.Length == 8)
                        return Color.FromArgb(
                            Convert.ToInt32(hex.Substring(0, 2), 16),
                            Convert.ToInt32(hex.Substring(2, 2), 16),
                            Convert.ToInt32(hex.Substring(4, 2), 16),
                            Convert.ToInt32(hex.Substring(6, 2), 16));
                }
                var named = Color.FromName(s);
                if (named.IsKnownColor) return named;
            }
            catch { }
            return fallback;
        }

        /// <summary>Rebuilds the bar contents from the given config.</summary>
        public void Rebuild(PinnedBarConfig config)
        {
            foreach (var i in ownedIcons)
            {
                try { i.Dispose(); } catch { }
            }
            ownedIcons.Clear();
            foreach (var b in ownedBitmaps)
            {
                try { b.Dispose(); } catch { }
            }
            ownedBitmaps.Clear();
            strip.Items.Clear();

            if (config?.Items == null || config.Items.Count == 0)
                return;

            int size = Math.Max(16, Math.Min(128, config.IconSize));
            strip.ImageScalingSize = new Size(size, size);

            Color bg = ParseColor(config.BackgroundColor, Color.FromArgb(240, 240, 240));
            BackColor = bg;
            strip.BackColor = bg;

            int padL = Math.Max(0, config.PaddingLeft);
            int padR = Math.Max(0, config.PaddingRight);
            int padT = Math.Max(0, config.PaddingTop);
            int padB = Math.Max(0, config.PaddingBottom);
            int padBetween = Math.Max(0, config.PaddingBetween);
            strip.Padding = new Padding(padL, padT, padR, padB);

            bool first = true;
            foreach (var item in config.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Path))
                    continue;

                string path = Environment.ExpandEnvironmentVariables(item.Path);
                Bitmap? bmp = null;
                if (!string.IsNullOrWhiteSpace(item.IconPath))
                {
                    string iconPath = Environment.ExpandEnvironmentVariables(item.IconPath);
                    bmp = TryLoadIconFromFile(iconPath, item.IconIndex, size);
                    if (bmp == null)
                        Debug.WriteLine($"PinnedBar iconPath failed, falling back to shell for '{path}' (iconPath='{iconPath}')");
                }
                if (bmp == null)
                    bmp = TryLoadIconBitmap(path, size);
                if (bmp != null) ownedBitmaps.Add(bmp);

                var btn = new ToolStripButton
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Image,
                    ImageScaling = ToolStripItemImageScaling.SizeToFit,
                    Image = bmp,
                    AutoToolTip = false,
                    ToolTipText = !string.IsNullOrWhiteSpace(item.Tooltip)
                        ? item.Tooltip
                        : Path.GetFileNameWithoutExtension(path),
                    Tag = path,
                    Margin = new Padding(first ? 0 : padBetween, 0, 0, 0),
                    Padding = new Padding(0),
                };
                btn.MouseDown += OnItemMouseDown;
                strip.Items.Add(btn);
                first = false;
            }

            var preferred = strip.GetPreferredSize(Size.Empty);
            int width = Math.Max(size + padL + padR, preferred.Width);
            int height = size + padT + padB;
            ClientSize = new Size(width, height);
        }

        /// <summary>
        /// Renders an unmanaged HICON into a fresh 32bpp ARGB Bitmap of the requested
        /// size, preserving the alpha channel. The caller is responsible for calling
        /// DestroyIcon on the hIcon itself.
        ///
        /// Uses Bitmap.FromHicon() to get the raw 32bpp ARGB bits (with correct alpha)
        /// and, if needed, scales into a target-size ARGB bitmap. Graphics.DrawIcon
        /// with stretching is unreliable here: it renders alpha as black for icons
        /// that come out of the system image list.
        /// </summary>
        private static Bitmap? DrawHIconToBitmap(IntPtr hIcon, int size)
        {
            if (hIcon == IntPtr.Zero) return null;

            try
            {
                // Prefer copying the icon's color DIB directly: this preserves the
                // 32bpp PARGB pixels the shell produced. Bitmap.FromHicon and
                // Icon.ToBitmap both mishandle PARGB icons for many shell items,
                // rendering the color channels as black where alpha < 255.
                Bitmap? source = TryBitmapFromIconInfo(hIcon);
                if (source == null)
                {
                    // Fallback: Icon.FromHandle(...).ToBitmap() (handles most cases in .NET)
                    using var ic = Icon.FromHandle(hIcon);
                    source = ic.ToBitmap();
                }

                if (source.Width == size && source.Height == size)
                    return source;

                var scaled = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                    g.Clear(Color.Transparent);
                    g.DrawImage(source, new Rectangle(0, 0, size, size));
                }
                source.Dispose();
                return scaled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PinnedBar DrawHIconToBitmap failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Copies the 32bpp color DIB out of the icon's ICONINFO into a managed
        /// ARGB Bitmap, converting from premultiplied alpha (PARGB) to straight
        /// ARGB. Returns null if the icon is not a 32bpp color icon.
        /// </summary>
        private static Bitmap? TryBitmapFromIconInfo(IntPtr hIcon)
        {
            ICONINFO info = default;
            if (!GetIconInfo(hIcon, out info)) return null;
            try
            {
                if (info.hbmColor == IntPtr.Zero) return null;

                using var color = Image.FromHbitmap(info.hbmColor);
                int w = color.Width, h = color.Height;

                // Image.FromHbitmap forgets the alpha channel. Re-read the raw
                // bits from the DIB by locking the color bitmap and copying,
                // then un-premultiply.
                var rect = new Rectangle(0, 0, w, h);
                var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var srcData = color.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var dstData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int stride = Math.Abs(srcData.Stride);
                    byte[] buf = new byte[stride * h];
                    Marshal.Copy(srcData.Scan0, buf, 0, buf.Length);

                    bool anyAlpha = false;
                    for (int i = 3; i < buf.Length; i += 4)
                    {
                        if (buf[i] != 0) { anyAlpha = true; break; }
                    }

                    if (!anyAlpha)
                    {
                        // Some icons have all-zero alpha in the color DIB - use the AND
                        // mask (hbmMask) to reconstruct opacity.
                        ApplyMask(info.hbmMask, buf, w, h, stride);
                    }
                    else
                    {
                        // Un-premultiply so GDI+ blends correctly with ARGB.
                        for (int i = 0; i < buf.Length; i += 4)
                        {
                            byte a = buf[i + 3];
                            if (a == 0)
                            {
                                buf[i] = buf[i + 1] = buf[i + 2] = 0;
                            }
                            else if (a < 255)
                            {
                                buf[i]     = (byte)Math.Min(255, buf[i]     * 255 / a);
                                buf[i + 1] = (byte)Math.Min(255, buf[i + 1] * 255 / a);
                                buf[i + 2] = (byte)Math.Min(255, buf[i + 2] * 255 / a);
                            }
                        }
                    }

                    Marshal.Copy(buf, 0, dstData.Scan0, buf.Length);
                }
                finally
                {
                    color.UnlockBits(srcData);
                    bmp.UnlockBits(dstData);
                }
                return bmp;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PinnedBar TryBitmapFromIconInfo failed: {ex.Message}");
                return null;
            }
            finally
            {
                if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                if (info.hbmMask  != IntPtr.Zero) DeleteObject(info.hbmMask);
            }
        }

        private static void ApplyMask(IntPtr hbmMask, byte[] buf, int w, int h, int stride)
        {
            if (hbmMask == IntPtr.Zero) return;
            try
            {
                using var mask = Image.FromHbitmap(hbmMask);
                var mrect = new Rectangle(0, 0, w, h);
                var md = mask.LockBits(mrect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int mstride = Math.Abs(md.Stride);
                    byte[] mbuf = new byte[mstride * h];
                    Marshal.Copy(md.Scan0, mbuf, 0, mbuf.Length);
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            int i = y * stride + x * 4;
                            int mi = y * mstride + x * 4;
                            // In the AND mask, 0 means opaque, non-zero means transparent.
                            byte mb = mbuf[mi];
                            buf[i + 3] = mb == 0 ? (byte)255 : (byte)0;
                        }
                    }
                }
                finally
                {
                    mask.UnlockBits(md);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PinnedBar ApplyMask failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to load an icon for the given path (typically a .lnk) and
        /// return it as a self-contained Bitmap. Tries, in order:
        /// 1. SHGetFileInfo(SHGFI_ICON | SHGFI_LARGEICON) - resolves .lnk targets via the shell.
        /// 2. ExtractIconEx(path, 0, large, null, 1) - direct icon resource extraction.
        /// 3. Icon.ExtractAssociatedIcon - .NET fallback.
        /// Each step is logged so we can see in the debug output which path succeeded.
        /// </summary>
        /// <summary>
        /// Loads an icon directly from an explicit file (typically .ico, .exe or .dll)
        /// at the requested size, honoring iconIndex for exe/dll resources.
        /// </summary>
        private static Bitmap? TryLoadIconFromFile(string iconPath, int iconIndex, int size)
        {
            if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
            {
                Debug.WriteLine($"PinnedBar iconPath: file NOT found '{iconPath}'");
                return null;
            }

            string ext = Path.GetExtension(iconPath).ToLowerInvariant();
            if (ext == ".ico")
            {
                try
                {
                    using var ic = new Icon(iconPath, size, size);
                    var bmp = DrawHIconToBitmap(ic.Handle, size);
                    if (bmp != null) return bmp;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PinnedBar iconPath (.ico) failed for '{iconPath}': {ex.Message}");
                }
            }

            // .exe / .dll (or fallback for .ico): extract by index
            try
            {
                var large = new IntPtr[1];
                uint count = ExtractIconEx(iconPath, iconIndex, large, null, 1);
                Debug.WriteLine($"PinnedBar iconPath (ExtractIconEx): '{iconPath}' idx={iconIndex} count={count} hIcon=0x{large[0].ToInt64():X}");
                if (count > 0 && large[0] != IntPtr.Zero)
                {
                    try { return DrawHIconToBitmap(large[0], size); }
                    finally { DestroyIcon(large[0]); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PinnedBar iconPath ExtractIconEx threw for '{iconPath}': {ex.Message}");
            }

            return null;
        }

        private static Bitmap? TryLoadIconBitmap(string path, int size)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.WriteLine("PinnedBar icon: empty path");
                return null;
            }
            if (!File.Exists(path))
            {
                Debug.WriteLine($"PinnedBar icon: file NOT found '{path}'");
                return null;
            }

            // (1) SHGetFileInfo
            try
            {
                var info = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(),
                    SHGFI_ICON | SHGFI_LARGEICON);
                Debug.WriteLine($"PinnedBar icon (SHGetFileInfo): '{path}' res=0x{res.ToInt64():X} hIcon=0x{info.hIcon.ToInt64():X}");
                if (info.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        var bmp = DrawHIconToBitmap(info.hIcon, size);
                        if (bmp != null) return bmp;
                    }
                    finally
                    {
                        DestroyIcon(info.hIcon);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PinnedBar SHGetFileInfo threw for '{path}': {ex.Message}");
            }

            // (2) ExtractIconEx - direct resource extraction, bypasses system imagelist
            try
            {
                var large = new IntPtr[1];
                uint count = ExtractIconEx(path, 0, large, null, 1);
                Debug.WriteLine($"PinnedBar icon (ExtractIconEx): '{path}' count={count} hIcon=0x{large[0].ToInt64():X}");
                if (count > 0 && large[0] != IntPtr.Zero)
                {
                    try
                    {
                        var bmp = DrawHIconToBitmap(large[0], size);
                        if (bmp != null) return bmp;
                    }
                    finally
                    {
                        DestroyIcon(large[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PinnedBar ExtractIconEx threw for '{path}': {ex.Message}");
            }

            // (3) Managed fallback
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    Debug.WriteLine($"PinnedBar icon (ExtractAssociatedIcon): '{path}' hIcon=0x{icon.Handle.ToInt64():X}");
                    return DrawHIconToBitmap(icon.Handle, size);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PinnedBar ExtractAssociatedIcon threw for '{path}': {ex.Message}");
            }

            Debug.WriteLine($"PinnedBar icon: ALL loaders failed for '{path}'");
            return null;
        }

        /// <summary>
        /// Shows the bar at the given screen location without activating it.
        /// Uses WinForms' Visible property (so the framework's internal state stays
        /// consistent) combined with SetWindowPos(HWND_TOPMOST, SWP_NOACTIVATE)
        /// to force the window back to the top of the Z-order after another
        /// topmost window (e.g. a newly launched app) took the foreground.
        /// </summary>
        public void ShowNoActivate(Point location)
        {
            _ = Handle; // force handle creation

            Location = location;

            if (!Visible)
            {
                // ShowWithoutActivation is respected by Form.Visible=true
                Visible = true;
            }

            SetWindowPos(Handle, HWND_TOPMOST, location.X, location.Y, Width, Height,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);

            Debug.WriteLine($"PinnedBar: ShowNoActivate at {location}, size ({Width}x{Height}), Visible={Visible}, IsWindowVisible={IsWindowVisible(Handle)}");
        }

        /// <summary>Hides the bar keeping WinForms' Visible state in sync.</summary>
        public void HideBar()
        {
            if (Visible)
            {
                Visible = false;
            }
        }

        private void OnItemMouseDown(object? sender, MouseEventArgs e)
        {
            if (sender is not ToolStripButton btn) return;
            if (btn.Tag is not string path || string.IsNullOrWhiteSpace(path)) return;

            if (e.Button == MouseButtons.Left)
            {
                // Close menus so activation of the launched app is clean, then
                // destroy this bar window entirely. The next activation will
                // create a fresh PinnedBarWindow instance in the caller.
                MenuActionHandler.CloseMenus();
                LaunchShortcut(path);
                BeginInvoke(new Action(() => Dispose()));
            }
            else if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu(path);
            }
        }

        private static void LaunchShortcut(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,          // point at the .lnk
                    UseShellExecute = true,   // required for .lnk + AUMID handling
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PinnedBar launch failed for '{path}': {ex.Message}");
                MessageBox.Show($"Failed to launch:\n{path}\n\n{ex.Message}",
                    "FlyMenu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowContextMenu(string path)
        {
            var ctx = new ContextMenuStrip();

            ctx.Items.Add("Open", null, (s, e) =>
            {
                MenuActionHandler.CloseMenus();
                LaunchShortcut(path);
                BeginInvoke(new Action(() => Dispose()));
            });

            ctx.Items.Add("Open file location", null, (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                }
                catch (Exception ex) { Debug.WriteLine(ex.Message); }
            });

            ctx.Items.Add("Copy path", null, (s, e) =>
            {
                try { Clipboard.SetText(path); } catch { }
            });

            ctx.Show(Cursor.Position);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var i in ownedIcons)
                {
                    try { i.Dispose(); } catch { }
                }
                ownedIcons.Clear();
                foreach (var b in ownedBitmaps)
                {
                    try { b.Dispose(); } catch { }
                }
                ownedBitmaps.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
