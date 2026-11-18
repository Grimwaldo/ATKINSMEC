using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ATKINSMEC
{
    // The Equipment Changer's one window (user, 2026-11-18: "instead of
    // using a big window ... select the element to be changed then after
    // pressing finish it will give a list of the types of the family ...
    // the window is very simple with ok and cancel buttons", resizable).
    //
    // Revit-free: it is handed the rows and hands back the one picked.
    // Built in code - a line, a list, OK and Cancel - all docked, so it
    // resizes without anchors to get wrong.
    public class EQUIPPICK : Form
    {
        public class Row
        {
            public long Id;
            public string Label = string.Empty;

            // A type a picked unit already has
            public bool Current;

            public override string ToString()
            {
                return Current ? Label + "   (current)" : Label;
            }
        }

        private readonly ListBox _list = new ListBox();
        private readonly Button _ok = new Button();
        private readonly Button _cancel = new Button();

        // The type picked, when OK is pressed
        public long Chosen { get; private set; }

        public EQUIPPICK(string said, List<Row> rows)
        {
            Text = "Atkins MEP Toolkit - Equipment Changer";
            AutoScaleMode = AutoScaleMode.Font;
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(380, 420);
            MinimumSize = new Size(300, 260);

            var top = new Label
            {
                Text = said,
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 40,
                Padding = new Padding(8, 8, 8, 0)
            };

            _list.Dock = DockStyle.Fill;
            _list.IntegralHeight = false;
            _list.Margin = new Padding(8);

            foreach (Row row in rows)
                _list.Items.Add(row);

            _list.SelectedIndexChanged += delegate { _ok.Enabled = Picked() != null; };
            _list.DoubleClick += delegate { Done(); };

            var holder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 0) };
            holder.Controls.Add(_list);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 44,
                Padding = new Padding(4, 8, 4, 4)
            };

            _cancel.Text = "Cancel";
            _cancel.DialogResult = DialogResult.Cancel;
            _cancel.AutoSize = true;
            _cancel.MinimumSize = new Size(80, 0);

            _ok.Text = "OK";
            _ok.Enabled = false;
            _ok.AutoSize = true;
            _ok.MinimumSize = new Size(80, 0);
            _ok.Click += delegate { Done(); };

            buttons.Controls.Add(_cancel);
            buttons.Controls.Add(_ok);

            // Fill first, then the edges, so the list takes what is left
            Controls.Add(holder);
            Controls.Add(buttons);
            Controls.Add(top);

            AcceptButton = _ok;
            CancelButton = _cancel;
        }

        private Row Picked()
        {
            return _list.SelectedItem as Row;
        }

        private void Done()
        {
            Row row = Picked();

            if (row == null)
                return;

            Chosen = row.Id;

            DialogResult = DialogResult.OK;

            Close();
        }
    }

    // Revit's main window as an owner for a WinForms dialog
    public class EQUIPOWNER : IWin32Window
    {
        public EQUIPOWNER(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; private set; }
    }
}
