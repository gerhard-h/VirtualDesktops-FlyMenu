using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FlyMenu
{
    /// <summary>
    /// A borderless, click-through, non-activating overlay form that draws a colored
    /// stripe at the top edge of the primary screen to indicate the hot area.
    ///
    /// Key properties:
    /// - Never becomes the foreground/active window (WS_EX_NOACTIVATE + ShowWithoutActivation).
    /// - Not shown in taskbar or Alt-Tab (WS_EX_TOOLWINDOW + ShowInTaskbar=false).
    /// - Fully click-through (WS_EX_LAYERED + WS_EX_TRANSPARENT), so clicks and mouse
    ///   events pass to windows underneath (e.g. maximized-window title bar buttons).
    /// - Always on top (TopMost).
    /// From Windows' perspective this window is a passive overlay; it does not affect
    /// which application is "current".
    /// </summary>
    internal sealed class HotAreaIndicator : Form
    {
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_LAYERED   = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOPMOST   = 0x00000008;

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE    = 0x0003;

        public HotAreaIndicator()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ControlBox = false;
            MinimizeBox = false;
            MaximizeBox = false;
            Enabled = false; // no input processing
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED
                              | WS_EX_TRANSPARENT | WS_EX_TOPMOST;
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

        /// <summary>
        /// Applies the configured HotArea indicator settings. Hides the form if
        /// disabled (height &lt;= 0) or edge != "top".
        /// </summary>
        public void ApplyConfig(HotAreaConfig hotArea)
        {
            if (hotArea == null)
            {
                Hide();
                return;
            }

            string edge = hotArea.Edge?.ToLowerInvariant() ?? "top";
            int height = hotArea.IndicatorHeight;

            if (edge != "top" || height <= 0)
            {
                if (Visible) Hide();
                return;
            }

            var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
            var area = screen.Bounds; // stripe sits above the working area, at the very top edge

            int startPct = Math.Max(0, Math.Min(100, hotArea.StartPercentage));
            int endPct = Math.Max(0, Math.Min(100, hotArea.EndPercentage));
            if (startPct > endPct) (startPct, endPct) = (endPct, startPct);

            int left = area.Left + (int)(area.Width * startPct / 100.0);
            int right = area.Left + (int)(area.Width * endPct / 100.0);
            int width = Math.Max(1, right - left);

            BackColor = ParseColor(hotArea.IndicatorColor);
            Opacity = Math.Max(0.0, Math.Min(1.0, hotArea.IndicatorOpacity));

            Bounds = new Rectangle(left, area.Top, width, height);

            if (!Visible)
            {
                Show();
            }
        }

        private static Color ParseColor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Color.Red;

            value = value.Trim();
            try
            {
                if (value.StartsWith("#"))
                {
                    var hex = value.Substring(1);
                    if (hex.Length == 6 &&
                        int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                    {
                        return Color.FromArgb(
                            (rgb >> 16) & 0xFF,
                            (rgb >> 8) & 0xFF,
                            rgb & 0xFF);
                    }
                    if (hex.Length == 8 &&
                        uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
                    {
                        return Color.FromArgb(
                            (int)((argb >> 24) & 0xFF),
                            (int)((argb >> 16) & 0xFF),
                            (int)((argb >> 8) & 0xFF),
                            (int)(argb & 0xFF));
                    }
                }
                else
                {
                    var named = Color.FromName(value);
                    if (named.IsKnownColor) return named;
                }
            }
            catch
            {
                // fall through to default
            }
            return Color.Red;
        }
    }
}
