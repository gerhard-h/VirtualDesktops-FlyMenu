using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

namespace FlyMenu
{
    /// <summary>
    /// Shared icon loader used by both the classic menu items and the pinned bar.
    /// Resolves logical icon references (bare file names, .ico files, .exe/.dll
    /// resources) into managed <see cref="Bitmap"/>s, and can extract shell icons
    /// from launchable paths (.lnk, .exe) as a fallback.
    ///
    /// Rendering path is PARGB-aware so shell icons with alpha don't render as
    /// black rectangles - see <see cref="RenderHIcon"/>.
    /// </summary>
    internal static class IconLoader
    {
        // ---------- P/Invoke ----------

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

        private static string IconsFolder =>
            Path.Combine(AppContext.BaseDirectory, "icons");

        // ---------- Public API ----------

        /// <summary>
        /// Resolves a logical icon reference (bare filename in <c>icons\</c>,
        /// absolute path, .ico / .exe / .dll) to a bitmap at the requested size.
        /// Returns null if the icon can't be loaded.
        /// </summary>
        public static Bitmap? LoadBitmap(string? iconRef, int iconIndex, int size)
        {
            if (string.IsNullOrWhiteSpace(iconRef)) return null;

            string resolved = ResolveIconPath(Environment.ExpandEnvironmentVariables(iconRef));
            if (!File.Exists(resolved))
            {
                Debug.WriteLine($"IconLoader: file NOT found '{resolved}' (ref='{iconRef}')");
                return null;
            }

            string ext = Path.GetExtension(resolved).ToLowerInvariant();
            if (ext == ".ico")
            {
                // For real .ico files the resource is already well-formed:
                // Icon.ToBitmap gives a correct managed ARGB bitmap. Going
                // through GetIconInfo/PARGB un-premultiply here can turn
                // the transparent areas black.
                try
                {
                    using var ic = new Icon(resolved, size, size);
                    return ic.ToBitmap();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"IconLoader (.ico) failed for '{resolved}': {ex.Message}");
                }
            }

            // .exe / .dll (or fallback for .ico): extract by index
            try
            {
                var large = new IntPtr[1];
                uint count = ExtractIconEx(resolved, iconIndex, large, null, 1);
                if (count > 0 && large[0] != IntPtr.Zero)
                {
                    try { return RenderHIcon(large[0], size); }
                    finally { DestroyIcon(large[0]); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IconLoader ExtractIconEx failed for '{resolved}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Loads a bitmap by asking the shell to extract the icon for the given
        /// launchable path (.lnk, .exe, ...). Used as a fallback when no explicit
        /// icon is configured. Returns null if the path is not a real file.
        /// </summary>
        public static Bitmap? LoadShellIconBitmap(string path, int size)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (!File.Exists(path)) return null;

            try
            {
                var info = new SHFILEINFO();
                IntPtr res = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(),
                    SHGFI_ICON | SHGFI_LARGEICON);
                if (info.hIcon != IntPtr.Zero)
                {
                    try { return RenderHIcon(info.hIcon, size); }
                    finally { DestroyIcon(info.hIcon); }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IconLoader SHGetFileInfo failed for '{path}': {ex.Message}");
            }

            try
            {
                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null) return RenderHIcon(icon.Handle, size);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IconLoader ExtractAssociatedIcon failed for '{path}': {ex.Message}");
            }

            return null;
        }

        // ---------- Resolution ----------

        private static string ResolveIconPath(string iconPath)
        {
            if (iconPath.Contains(Path.DirectorySeparatorChar) ||
                iconPath.Contains(Path.AltDirectorySeparatorChar))
            {
                return iconPath;
            }

            string inIconsFolder = Path.Combine(IconsFolder, iconPath);
            if (File.Exists(inIconsFolder)) return inIconsFolder;

            return iconPath;
        }

        // ---------- HICON -> Bitmap (PARGB safe) ----------

        private static Bitmap? RenderHIcon(IntPtr hIcon, int size)
        {
            if (hIcon == IntPtr.Zero) return null;

            try
            {
                Bitmap? source = BitmapFromIconInfo(hIcon);
                if (source == null)
                {
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
                Debug.WriteLine($"IconLoader RenderHIcon failed: {ex.Message}");
                return null;
            }
        }

        private static Bitmap? BitmapFromIconInfo(IntPtr hIcon)
        {
            if (!GetIconInfo(hIcon, out ICONINFO info)) return null;
            try
            {
                if (info.hbmColor == IntPtr.Zero) return null;

                using var color = Image.FromHbitmap(info.hbmColor);
                int w = color.Width, h = color.Height;
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
                        ApplyMask(info.hbmMask, buf, w, h, stride);
                    }
                    else
                    {
                        // Un-premultiply
                        for (int i = 0; i < buf.Length; i += 4)
                        {
                            byte a = buf[i + 3];
                            if (a == 0) { buf[i] = buf[i + 1] = buf[i + 2] = 0; }
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
                Debug.WriteLine($"IconLoader BitmapFromIconInfo failed: {ex.Message}");
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
                            buf[i + 3] = mbuf[mi] == 0 ? (byte)255 : (byte)0;
                        }
                    }
                }
                finally { mask.UnlockBits(md); }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IconLoader ApplyMask failed: {ex.Message}");
            }
        }
    }
}
