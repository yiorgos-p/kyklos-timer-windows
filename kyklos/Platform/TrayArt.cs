using Kyklos.Engine;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Kyklos.Platform;

/// <summary>
/// Draws the tray icon. Windows 11's own tray icons are monochrome and follow the taskbar's theme, which can differ from
/// kyklos's: the kyklos glyph while stopped, and during a session a ring with a pie inside for the time left, as the
/// Mac's menu bar circle.
/// </summary>
internal static class TrayArt
{
    /// <summary>Whether the taskbar is light, which can differ from the apps' theme.</summary>
    public static bool TaskbarIsLight()
    {
        using var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return personalize?.GetValue("SystemUsesLightTheme") is 1;
    }

    /// <summary>The size of a tray icon, in pixels, at the taskbar's scale: 16 at 100%, 20 at 125%, 24 at 150%, 32 at 200%.</summary>
    public static int IconSize() => PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, PInvoke.GetDpiForSystem());

    public static unsafe HICON Glyph(bool lightTaskbar, int size)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Assets", "Tray", lightTaskbar ? "tray-for-light-taskbar.ico" : "tray-for-dark-taskbar.ico");
        fixed (char* path = file)
        {
            return (HICON)(nint)PInvoke.LoadImage(HINSTANCE.Null, path, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, IMAGE_FLAGS.LR_LOADFROMFILE).Value;
        }
    }

    public static HICON Ring(TrayTone tone, double fraction, bool lightTaskbar, int size)
    {
        var (color, alpha) = Color(tone, lightTaskbar);
        var pixels = new uint[size * size];

        // At 16 px: a ring 1.5 px wide, inset 1.5 px, with a pie 2 px inside it. Larger icons scale it up.
        var scale = size / 16.0;
        var center = size / 2.0;
        var ringRadius = (size - 3 * scale) / 2;
        var halfWidth = 0.75 * scale;
        var pieRadius = ringRadius - 2 * scale;
        var pieAngle = fraction * 2 * Math.PI;

        // Four by four samples a pixel, for smooth edges.
        const int samples = 4;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var covered = 0;

                for (var sy = 0; sy < samples; sy++)
                {
                    for (var sx = 0; sx < samples; sx++)
                    {
                        var dx = x + (sx + 0.5) / samples - center;
                        var dy = y + (sy + 0.5) / samples - center;
                        var distance = Math.Sqrt(dx * dx + dy * dy);

                        // Clockwise from 12 o'clock.
                        var angle = Math.Atan2(dx, -dy);
                        if (angle < 0)
                        {
                            angle += 2 * Math.PI;
                        }

                        if (Math.Abs(distance - ringRadius) <= halfWidth || (distance <= pieRadius && angle <= pieAngle))
                        {
                            covered++;
                        }
                    }
                }

                var opacity = alpha * covered / (samples * samples);
                pixels[y * size + x] = Premultiplied(color, opacity);
            }
        }

        return Icon(pixels, size);
    }

    /// <summary>The colours Windows uses on dark and light taskbars, tuned like the Mac's system green, yellow and red.</summary>
    private static (uint Rgb, double Alpha) Color(TrayTone tone, bool lightTaskbar) => tone switch
    {
        TrayTone.Green => (lightTaskbar ? 0x0F7B0Fu : 0x6CCB5Fu, 1),
        TrayTone.Yellow => (lightTaskbar ? 0x9D5D00u : 0xFCE100u, 1),
        TrayTone.Red => (lightTaskbar ? 0xC42B1Cu : 0xFF99A4u, 1),
        // Paused: the taskbar's text colour at 60%.
        _ => (lightTaskbar ? 0x000000u : 0xFFFFFFu, 0.6),
    };

    private static uint Premultiplied(uint rgb, double opacity)
    {
        var alpha = (uint)Math.Round(opacity * 255);
        uint Channel(int shift) => (uint)Math.Round(((rgb >> shift) & 0xFF) * opacity);
        return alpha << 24 | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }

    /// <summary>A 32-bit icon from premultiplied BGRA pixels, top row first.</summary>
    private static unsafe HICON Icon(uint[] pixels, int size)
    {
        var header = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = size,
                biHeight = -size,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
            },
        };

        void* bits;
        var color = PInvoke.CreateDIBSection(HDC.Null, &header, DIB_USAGE.DIB_RGB_COLORS, &bits, HANDLE.Null, 0);
        fixed (uint* source = pixels)
        {
            Buffer.MemoryCopy(source, bits, pixels.Length * 4L, pixels.Length * 4L);
        }

        var mask = PInvoke.CreateBitmap(size, size, 1, 1, null);
        var info = new ICONINFO { fIcon = true, hbmColor = color, hbmMask = mask };
        var icon = PInvoke.CreateIconIndirect(&info);

        PInvoke.DeleteObject(color);
        PInvoke.DeleteObject(mask);
        return icon;
    }
}
