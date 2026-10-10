using System;
using System.Windows;
using System.Windows.Controls;

namespace ATKINSMEC
{
    // AUTOVENT'S WINDOW ON A SMALL SCREEN. It was 860 tall with a minimum
    // of 800, and a 1920 x 1080 laptop at 150% has about 690 above the
    // taskbar - so it could not fit at all. Now it opens no bigger than the
    // screen, may be made smaller, and its body scrolls once it is shorter
    // than its rows need.
    //
    // Revit-free, so tests/ventpreview's stub runs the same code.
    public static class VENTFIT
    {
        // THE BODY AS TALL AS THE WINDOW GIVES IT, never less than its rows
        // need - below that, the scroll viewer round it scrolls.
        //
        // Its height is SET, never left to the scroll viewer: a scroll viewer
        // measures its content with unlimited height, which turns the levels
        // row's * into Auto - the tree would then build every row it has and
        // lose its virtualisation. Rows 0 and 2 are Auto; row 1 is the
        // levels card, held to its MinHeight.
        //
        // ONCE IT SCROLLS ANYWAY, the levels row gets `Roomy` rather than
        // its bare minimum: at the minimum the tree shows three rooms, and
        // scrolling the window to see more of a list too short to use is
        // the worse of the two.
        public const double Roomy = 300;

        public static void Body(ScrollViewer scroll, Grid body)
        {
            if (scroll == null || body == null || body.RowDefinitions.Count < 3)
                return;

            double fixedRows = body.RowDefinitions[0].ActualHeight + body.RowDefinitions[2].ActualHeight;
            double needed = fixedRows + body.RowDefinitions[1].MinHeight;

            double room = scroll.ActualHeight - body.Margin.Top - body.Margin.Bottom;

            double height = room >= needed
                ? room
                : fixedRows + Math.Max(body.RowDefinitions[1].MinHeight, Roomy);

            if (height > 0 && (double.IsNaN(body.Height) || Math.Abs(body.Height - height) > 0.5))
                body.Height = height;
        }

        // No bigger than the screen's working area, and never asked to be
        // smaller than it can be. Called before the window is shown, so
        // centring it still centres it.
        public static void Screen(Window window)
        {
            if (window == null)
                return;

            Rect work = SystemParameters.WorkArea;

            if (window.Height > work.Height)
                window.Height = work.Height;

            if (window.Width > work.Width)
                window.Width = work.Width;

            window.MinHeight = Math.Min(window.MinHeight, window.Height);
            window.MinWidth = Math.Min(window.MinWidth, window.Width);
        }
    }
}
