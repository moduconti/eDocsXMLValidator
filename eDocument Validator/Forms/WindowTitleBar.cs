using System;
using System.Runtime.InteropServices;

namespace eDocument_Validator.Forms
{
    /// <summary>
    /// Asks Windows to draw the title bar of a window in its dark colours.
    /// <para>
    /// Windows Forms colours only the inside of a window. The title bar belongs to
    /// Windows, and without this a dark window keeps a bright white title bar,
    /// which looks broken. The setting exists on Windows 10 version 1809 and later;
    /// on older versions the call simply does nothing.
    /// </para>
    /// </summary>
    internal static class WindowTitleBar
    {
        /// <summary>Attribute number used by Windows 10 version 2004 and later.</summary>
        private const int UseImmersiveDarkMode = 20;

        /// <summary>Attribute number used by earlier Windows 10 versions.</summary>
        private const int UseImmersiveDarkModeBeforeWindows20H1 = 19;

        [DllImport("dwmapi.dll", SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);

        public static void UseDarkColours(IntPtr windowHandle, bool dark)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return;
            }

            int value = dark ? 1 : 0;

            try
            {
                if (DwmSetWindowAttribute(windowHandle, UseImmersiveDarkMode, ref value, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(windowHandle, UseImmersiveDarkModeBeforeWindows20H1, ref value, sizeof(int));
                }
            }
            catch (DllNotFoundException)
            {
                // The window keeps its normal title bar. Nothing else is affected.
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }
}
