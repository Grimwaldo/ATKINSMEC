using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ATKINSMEC
{
    // Does a tag SHOW a flow? The flowrate check reports a tag on an
    // element with no flow only when it does (user, 2026-11-10: "check
    // the text along with the flowrate, if its showing a text that doesnt
    // display flow then dont report it" - a riser tag reading "500x400
    // EAD FA/TB", size and system, was listed as a tag reading zero).
    // Revit-free; the check hands it what tags read
    // (IndependentTag.TagText) and the flows. Test: flowtagtest.
    //
    // The API cannot say which parameters a tag's labels show, so two
    // things are read, and EITHER is enough to report:
    //  - the tag's own text (ReadsZero): a flow unit ("L/s", "CFM",
    //    "m³/h" ...) or a zero standing on its own ("0", "0.0", "Q=0") -
    //    never a digit of a size ("500x400"), a level ("L00"), an
    //    elevation ("+0.00") or a zero with a unit that is not a flow's
    //    ("0 mm", "0 Pa");
    //  - the other tags of its TYPE on elements that DO carry flow
    //    (Shows, Evidence): when most of them read their element's flow,
    //    the type shows flow. That catches what the text alone cannot -
    //    an unset flow may print nothing, and TagText runs several labels
    //    together, so a zero can be glued to the size before it
    //    ("500x4000").
    public static class QAFLOWTEXT
    {
        // How many other tags of a type are read to decide it
        public const int EvidenceCap = 40;

        // A type's record: Seen = its tags on elements with a flow whose
        // text could be read, Showed = how many read that flow
        public class Evidence
        {
            public int Seen { get; set; }
            public int Showed { get; set; }

            public bool ShowsFlow
            {
                get { return Showed > 0 && Showed * 2 > Seen; }
            }
        }

        // Report a tag on an element whose flow is zero or not set?
        // text = what it reads, null when Revit would not say.
        public static bool Report(string text, Evidence evidence)
        {
            if (text != null && ReadsZero(text))
                return true;

            if (evidence != null && evidence.ShowsFlow)
                return true;

            // Nothing to go on at all: as before this rule, reported -
            // a check that cannot read a tag must not go quiet about it
            if (text == null && (evidence == null || evidence.Seen == 0))
                return true;

            return false;
        }

        // The tag on an element with no flow: does its text read as a
        // flow? A flow unit, or a zero on its own.
        public static bool ReadsZero(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            if (HasFlowUnit(text))
                return true;

            foreach (Match m in Number.Matches(text))
            {
                if (!AllZero(m.Value))
                    continue;

                int before = m.Index - 1;
                int after = m.Index + m.Length;

                // A level "L00", a diameter "Ø0", "400x0", an elevation
                // "+0.00"
                if (before >= 0)
                {
                    char c = text[before];

                    if (char.IsLetter(c) || c == '+')
                        continue;
                }

                if (after < text.Length && IsTimes(text[after]) &&
                    after + 1 < text.Length && char.IsDigit(text[after + 1]))
                {
                    continue;
                }

                if (OtherUnitAt(text, after))
                    continue;

                return true;
            }

            return false;
        }

        // A tag on an element WITH a flow: does its text show that flow?
        // cfs = Revit's internal value (ft³/s); shown = the parameter's
        // own AsValueString, in the project's units ("250 L/s"), which is
        // what a label using the project's settings prints. A label with
        // units of its own is tried in each flow unit, 0 to 3 decimals.
        public static bool Shows(string text, double cfs, string shown)
        {
            if (string.IsNullOrEmpty(text) || Math.Abs(cfs) < 1e-12)
                return false;

            foreach (string candidate in Candidates(cfs, shown))
            {
                if (ContainsNumber(text, candidate))
                    return true;
            }

            return false;
        }

        // ---------------------------------------------------------------

        // Revit's ft³/s in each unit a flow label may use: L/s, m³/h,
        // CFM, US gpm, L/min, m³/s
        private static readonly double[] PerCfs =
        {
            28.316846592,
            101.9406477312,
            60.0,
            448.831168831,
            1699.01079552,
            0.028316846592
        };

        private static readonly string[] FlowUnits =
        {
            "l/s", "ℓ/s", "lps", "l/min", "lpm", "l/h",
            "m³/h", "m3/h", "m³/hr", "m3/hr", "cmh",
            "m³/s", "m3/s", "m³/min", "m3/min",
            "cfm", "gpm", "usgpm"
        };

        // A zero followed by one of these is not a flow: millimetres,
        // metres (also "m²", "m³" alone), velocity, pressure, angles
        private static readonly string[] OtherUnits =
        {
            "mm", "cm", "m/s", "m", "ft", "in", "fpm", "pa", "kpa",
            "%", "°", "'", "\""
        };

        // A run of digits with its decimal or thousands separators
        private static readonly Regex Number =
            new Regex(@"\d+(?:[.,]\d+)*", RegexOptions.CultureInvariant);

        private static bool AllZero(string number)
        {
            foreach (char c in number)
            {
                if (c != '0' && c != '.' && c != ',')
                    return false;
            }

            return true;
        }

        private static bool IsTimes(char c)
        {
            return c == 'x' || c == 'X' || c == '×' || c == '*';
        }

        private static bool HasFlowUnit(string text)
        {
            string lower = text.ToLowerInvariant();

            foreach (string unit in FlowUnits)
            {
                int at = 0;

                while ((at = lower.IndexOf(unit, at, StringComparison.Ordinal)) >= 0)
                {
                    bool startOk = at == 0 || !char.IsLetter(lower[at - 1]);
                    int end = at + unit.Length;
                    bool endOk = end >= lower.Length || !char.IsLetter(lower[end]);

                    if (startOk && endOk)
                        return true;

                    at++;
                }
            }

            return false;
        }

        // A unit that is not a flow's at this point of the text, after
        // any spaces. A flow unit there is not one of them ("0 m³/h").
        private static bool OtherUnitAt(string text, int at)
        {
            while (at < text.Length && text[at] == ' ')
                at++;

            if (at >= text.Length)
                return false;

            string rest = text.Substring(at).ToLowerInvariant();

            foreach (string unit in FlowUnits)
            {
                if (rest.StartsWith(unit, StringComparison.Ordinal))
                    return false;
            }

            foreach (string unit in OtherUnits)
            {
                if (!rest.StartsWith(unit, StringComparison.Ordinal))
                    continue;

                // "0 INLET" is not inches, "0 MAX" not metres
                if (char.IsLetter(unit[unit.Length - 1]) &&
                    rest.Length > unit.Length && char.IsLetter(rest[unit.Length]))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        // The numbers a label might print for this flow
        private static IEnumerable<string> Candidates(double cfs, string shown)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (!string.IsNullOrEmpty(shown))
            {
                Match m = Number.Match(shown);

                if (m.Success && !AllZero(m.Value) && seen.Add(m.Value))
                    yield return m.Value;
            }

            foreach (double factor in PerCfs)
            {
                double value = Math.Abs(cfs * factor);

                for (int decimals = 0; decimals <= 3; decimals++)
                {
                    string dot = value.ToString(
                        "F" + decimals, CultureInfo.InvariantCulture);

                    // A small flow in m³/s rounds to "0.00" - that would
                    // be found in any text with a zero in it
                    if (AllZero(dot))
                        continue;

                    if (seen.Add(dot))
                        yield return dot;

                    string comma = dot.Replace('.', ',');

                    if (seen.Add(comma))
                        yield return comma;
                }
            }
        }

        // The number appears in the text and does not run on into more
        // digits ("50" is not in "500x400"). It may be glued on the LEFT,
        // since TagText runs labels together ("500x400" + "250 L/s").
        private static bool ContainsNumber(string text, string number)
        {
            int at = 0;

            while ((at = text.IndexOf(number, at, StringComparison.Ordinal)) >= 0)
            {
                int end = at + number.Length;

                bool runsOn = end < text.Length &&
                    (char.IsDigit(text[end]) ||
                     ((text[end] == '.' || text[end] == ',') &&
                      end + 1 < text.Length && char.IsDigit(text[end + 1])));

                // The decimals of another number: "1.250" holds no 250
                bool fraction = at >= 2 &&
                    (text[at - 1] == '.' || text[at - 1] == ',') &&
                    char.IsDigit(text[at - 2]);

                if (!runsOn && !fraction)
                    return true;

                at++;
            }

            return false;
        }
    }
}
