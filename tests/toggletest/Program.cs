using System;
using System.ComponentModel;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;

internal class Row : INotifyPropertyChanged
{
    private bool? _c;
    public bool? Checked { get { return _c; } set { if (_c == value) return; _c = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Checked")); } }
    public event PropertyChangedEventHandler PropertyChanged;
}

internal static class Program
{
    private static string S(bool? b) { return b?.ToString() ?? "null"; }

    [STAThread]
    private static void Main()
    {
        foreach (bool? start in new bool?[] { null, false, true })
        {
            var row = new Row { Checked = start };
            var box = new CheckBox { IsThreeState = false };
            box.SetBinding(CheckBox.IsCheckedProperty, new Binding("Checked") { Source = row, Mode = BindingMode.OneWay });

            // what a level's box does in Levels and rooms (VENTROOMSWINDOW.LevelClicked)
            box.Click += (s, e) => { row.Checked = row.Checked != true; };

            string before = S(box.IsChecked);
            typeof(System.Windows.Controls.Primitives.ToggleButton).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(box, null);
            Console.WriteLine("start " + S(start) + " (box " + before + ") -> row " + S(row.Checked) + ", box " + S(box.IsChecked)
                + ", binding kept " + (BindingOperations.GetBindingExpression(box, CheckBox.IsCheckedProperty) != null));

            // the row changed from elsewhere (All / None) - does the box still follow?
            row.Checked = false;
            Console.WriteLine("    then row set false elsewhere -> box " + S(box.IsChecked));
            row.Checked = null;
            Console.WriteLine("    then row set null (settled)  -> box " + S(box.IsChecked));
        }
    }
}
