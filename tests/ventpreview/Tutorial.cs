using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ventpreview
{
    // The shots the AUTOVENT tutorial video is cut from.
    //
    // Rendered at 1.5x so the video can zoom into a card without the
    // text going soft, and each shot comes with a JSON of where every
    // named control sits - taken from WPF's own layout, so the video's
    // highlight boxes land on the controls exactly rather than on
    // coordinates somebody measured off a screenshot.
    internal static class Tutorial
    {
        public const double Scale = 1.5;

        private static readonly string[] Names =
        {
            "ListLinks", "BtnRefreshLinks", "TxtLinksInfo",
            "BtnImport", "TxtSheetPath", "CboWorksheet", "TxtSheetInfo",
            "ListLevels", "BtnAllLevels", "BtnNoLevels", "TxtLevelsInfo",
            "TxtLevelsWarning", "BtnClose", "BtnShowRoom",
            "TabActions", "TxtFreshTypes", "LstFresh", "TxtExtractTypes",
            "LstExtract", "TxtEndcapReach", "BtnRun",
            "RdoIntoImported", "CboExportSheet", "RdoNewWorkbook",
            "RdoAddWorksheet", "TxtNewSheet", "BtnExport", "TxtExportHint"
        };

        public static void Shoot(ATKINSMEC.VENTWindow window, string folder)
        {
            window.Fill();

            // The levels summary in the stub predates the change to the
            // main model's levels - show what the window says NOW
            window.Levels(9, 4, 1, 5, 4, "MEZZANINE");

            window.ShowAssign();
            window.PickRoom();
            Take(window, Path.Combine(folder, "tut_assign"));

            window.ShowExport();
            Take(window, Path.Combine(folder, "tut_export"));

            // The export tab set up for a NEW workbook - the way that
            // needs nothing imported
            window.TutorialNewWorkbook();
            Take(window, Path.Combine(folder, "tut_export_new"));

            window.ShowAssign();
            window.Levels(9, 2, 1, 12, 2, "B1_BASEMENT", "B2_BASEMENT",
                "MEZZANINE", "P5_PODIUM 5", "P6_PODIUM 6", "P7_PODIUM 7",
                "ROOF", "TOP OF PARAPET", "PLANT DECK", "LIFT OVERRUN");
            Take(window, Path.Combine(folder, "tut_levels_many"));
        }

        private static void Take(Window window, string stem)
        {
            window.UpdateLayout();

            window.Dispatcher.Invoke(
                new Action(delegate { }),
                System.Windows.Threading.DispatcherPriority.ContextIdle);

            int width = (int)Math.Round(window.ActualWidth * Scale);
            int height = (int)Math.Round(window.ActualHeight * Scale);

            var bitmap = new RenderTargetBitmap(
                width, height, 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);

            bitmap.Render(window);

            var encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (FileStream file = File.Create(stem + ".png"))
                encoder.Save(file);

            // Where everything is, in the PNG's own pixels
            var json = new StringBuilder();

            json.Append("{\n  \"size\": [" + width + ", " + height + "]");

            foreach (string name in Names)
            {
                var element = window.FindName(name) as FrameworkElement;

                if (element == null || !element.IsVisible)
                    continue;

                Add(json, name, Box(element, window));

                // ...and the card it sits in, for highlighting a whole
                // step at once
                Border card = CardOf(element, window);

                if (card != null)
                    Add(json, name + ".card", Box(card, window));
            }

            json.Append("\n}\n");

            File.WriteAllText(stem + ".json", json.ToString());

            Console.WriteLine("  " + Path.GetFileName(stem) + ".png  " +
                              width + "x" + height);
        }

        private static Rect Box(FrameworkElement element, Visual root)
        {
            Rect r = element.TransformToAncestor(root).TransformBounds(
                new Rect(0, 0, element.ActualWidth, element.ActualHeight));

            return new Rect(r.X * Scale, r.Y * Scale,
                            r.Width * Scale, r.Height * Scale);
        }

        private static void Add(StringBuilder json, string name, Rect r)
        {
            json.Append(",\n  \"" + name + "\": [" +
                        (int)r.X + ", " + (int)r.Y + ", " +
                        (int)Math.Ceiling(r.Right) + ", " +
                        (int)Math.Ceiling(r.Bottom) + "]");
        }

        // The white card each step lives on: the LARGEST rounded Border
        // above the element that is still clearly smaller than the
        // window. The innermost would be the list box's own frame; the
        // outermost could be the window's chrome.
        private static Border CardOf(DependencyObject element, Window window)
        {
            double limit =
                window.ActualWidth * window.ActualHeight * 0.6;

            Border found = null;
            double best = 0;

            DependencyObject at = VisualTreeHelper.GetParent(element);

            while (at != null)
            {
                var border = at as Border;

                if (border != null && border.CornerRadius.TopLeft > 0)
                {
                    double area = border.ActualWidth * border.ActualHeight;

                    if (area > best && area < limit)
                    {
                        best = area;
                        found = border;
                    }
                }

                at = VisualTreeHelper.GetParent(at);
            }

            return found;
        }
    }
}
