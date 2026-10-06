using System;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // Every window in the suite can be resized, maximised and
    // minimised. The border style and the two boxes are set in each
    // designer; this is the one thing that cannot sensibly go there.
    //
    // A window may not shrink below the size it was DESIGNED at,
    // because below that the controls start sitting on each other -
    // anchors move things, they do not reflow them. The designed size
    // is not a constant that can be typed into the designer either:
    // Revit runs at whatever the machine's scaling is, and WinForms
    // scales the whole form on the way up. So the minimum is taken
    // from the form itself once it has been built and scaled, which is
    // the first moment it is true.
    //
    // A form that already names a MinimumSize is left alone - the help
    // windows say a smaller one deliberately, being a text box that is
    // perfectly readable at half the size.
    public static class FORMSIZE
    {
        public static void Remember(Form form)
        {
            if (form == null)
                return;

            form.Load += Settle;
        }

        private static void Settle(object sender, EventArgs e)
        {
            var form = sender as Form;

            if (form == null)
                return;

            // Once only: this runs on Load, and a form is loaded once
            form.Load -= Settle;

            if (!form.MinimumSize.IsEmpty)
                return;

            try
            {
                form.MinimumSize = form.Size;
            }
            catch
            {
                // A minimum bigger than the screen is refused on some
                // machines; a window that cannot be shrunk to its
                // design size is not worth failing the command over
            }
        }
    }
}
