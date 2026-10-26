using System;
using System.Windows;

namespace ATKINSMEC
{
    // AUTOVENT'S WINDOWS ON A SMALL SCREEN. The main window was 860 tall
    // with a minimum of 800, and a 1920 x 1080 laptop at 150% has about
    // 690 above the taskbar - so it could not fit at all. Now no window
    // opens bigger than the screen, and the main window's body scrolls
    // once it is shorter than its content.
    //
    // It used to set the main window's body height as well, so the levels
    // tree kept its virtualisation inside the scroll viewer. The tree moved
    // to Levels and rooms (VENTROOMSWINDOW, 2026-10-19), and with it the
    // reason: the main window now opens as tall as its content
    // (SizeToContent), up to the screen.
    //
    // Revit-free, so tests/ventpreview's stub runs the same code.
    public static class VENTFIT
    {
        // No bigger than the screen's working area, and never asked to be
        // smaller than it can be. Called before the window is shown, so
        // centring it still centres it.
        //
        // A window sized to its content has no height yet: it is capped
        // while it finds one, then keeps that height and lets the cap go -
        // so it can still be dragged taller, on a bigger screen too - and
        // is moved up if it has grown past the bottom of the screen.
        public static void Screen(Window window)
        {
            if (window == null)
                return;

            Rect work = SystemParameters.WorkArea;

            if (!double.IsNaN(window.Height) && window.Height > work.Height)
                window.Height = work.Height;

            if (!double.IsNaN(window.Width) && window.Width > work.Width)
                window.Width = work.Width;

            window.MinHeight = Math.Min(window.MinHeight, double.IsNaN(window.Height) ? work.Height : window.Height);
            window.MinWidth = Math.Min(window.MinWidth, double.IsNaN(window.Width) ? work.Width : window.Width);

            if (window.SizeToContent != SizeToContent.Height && window.SizeToContent != SizeToContent.WidthAndHeight)
                return;

            double cap = window.MaxHeight;

            window.MaxHeight = Math.Min(cap, work.Height);

            EventHandler sized = null;

            sized = (s, e) =>
            {
                window.ContentRendered -= sized;

                double height = window.ActualHeight;

                window.SizeToContent = SizeToContent.Manual;
                window.Height = height;
                window.MaxHeight = cap;

                if (window.Top + height > work.Bottom)
                    window.Top = Math.Max(work.Top, work.Bottom - height);
            };

            window.ContentRendered += sized;
        }
    }
}
