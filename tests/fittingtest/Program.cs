using System;
using System.Collections.Generic;

// Mirrors the decisions in QACHWFITTINGS - which of Revit's five
// creation calls a replaced fitting needs, and which pipe is the branch
// of a tee - with no Revit present. Keep the two in step.
//
// This is the part that can be got wrong SILENTLY. A tee rebuilt with
// the wrong pipe as its branch still comes out a tee, still connects,
// and is wrong on the drawing. Nothing else would catch it.
internal static class Program
{
    private static int _failed;

    private static void Check(string what, bool ok)
    {
        Console.WriteLine((ok ? "  pass  " : "  FAIL  ") + what);

        if (!ok)
            _failed++;
    }

    private const double AgreeMm = 0.5;

    // Revit's PartType, as far as this cares
    private enum Kind { Undefined, Elbow, Tee, Cross, Transition, Union }

    private class XYZ
    {
        public double X, Y, Z;

        public XYZ(double x, double y, double z) { X = x; Y = y; Z = z; }

        public double Dot(XYZ o) { return X * o.X + Y * o.Y + Z * o.Z; }
    }

    private class Want
    {
        public double NowMm;
        public double WantMm;

        public bool Agrees
        {
            get { return Math.Abs(NowMm - WantMm) <= AgreeMm; }
        }
    }

    private static Want W(double now, double want)
    {
        return new Want { NowMm = now, WantMm = want };
    }

    // ==================================================
    // The mirrored decisions
    // ==================================================

    private static string Make(
        Kind kind, List<XYZ> axes, List<Want> wants)
    {
        if (axes.Count == 2)
        {
            bool straight = Math.Abs(axes[0].Dot(axes[1])) > 0.999;

            bool sameSize =
                Math.Abs(wants[0].WantMm - wants[1].WantMm) <= AgreeMm;

            if (kind == Kind.Elbow
                || (kind != Kind.Transition
                    && kind != Kind.Union
                    && !straight))
                return "elbow";

            return sameSize ? "union" : "transition";
        }

        if (axes.Count == 3)
            return "tee branch=" + Odd(axes);

        if (axes.Count == 4)
        {
            int partner = Partner(axes, 0);

            var rest = new List<int>();

            for (int i = 1; i < 4; i++)
            {
                if (i != partner)
                    rest.Add(i);
            }

            return "cross 0+" + partner + " " + rest[0] + "+" + rest[1];
        }

        return "refuse";
    }

    private static int Odd(List<XYZ> axes)
    {
        int one = 0;
        int two = 1;

        double best = -1;

        for (int i = 0; i < axes.Count; i++)
        {
            for (int j = i + 1; j < axes.Count; j++)
            {
                double parallel = Math.Abs(axes[i].Dot(axes[j]));

                if (parallel > best)
                {
                    best = parallel;

                    one = i;
                    two = j;
                }
            }
        }

        for (int i = 0; i < axes.Count; i++)
        {
            if (i != one && i != two)
                return i;
        }

        return axes.Count - 1;
    }

    private static int Partner(List<XYZ> axes, int of)
    {
        int best = of == 0 ? 1 : 0;

        double parallel = -1;

        for (int i = 0; i < axes.Count; i++)
        {
            if (i == of)
                continue;

            double how = Math.Abs(axes[of].Dot(axes[i]));

            if (how > parallel)
            {
                parallel = how;

                best = i;
            }
        }

        return best;
    }

    // ==================================================

    private static readonly XYZ East = new XYZ(1, 0, 0);
    private static readonly XYZ West = new XYZ(-1, 0, 0);
    private static readonly XYZ North = new XYZ(0, 1, 0);
    private static readonly XYZ South = new XYZ(0, -1, 0);
    private static readonly XYZ Up = new XYZ(0, 0, 1);

    private static List<XYZ> Axes(params XYZ[] axes)
    {
        return new List<XYZ>(axes);
    }

    private static List<Want> Sizes(params double[] mm)
    {
        var wants = new List<Want>();

        foreach (double one in mm)
            wants.Add(W(one, one));

        return wants;
    }

    private static int Main()
    {
        Console.WriteLine("CHW fitting-replacement harness");
        Console.WriteLine();

        // --- two ends ------------------------------------------------

        Check("two pipes meeting at a corner need an elbow",
            Make(Kind.Elbow, Axes(East, North), Sizes(50, 50)) == "elbow");

        Check("a family that will not say what it is, at a corner, is "
              + "still an elbow",
            Make(Kind.Undefined, Axes(East, North), Sizes(50, 50))
                == "elbow");

        Check("two pipes in line and the same size need a union",
            Make(Kind.Union, Axes(East, West), Sizes(50, 50)) == "union");

        Check("two pipes in line and different sizes need a reducer",
            Make(Kind.Transition, Axes(East, West), Sizes(80, 50))
                == "transition");

        Check("in line and different sizes is a reducer even when the "
              + "family will not say",
            Make(Kind.Undefined, Axes(East, West), Sizes(80, 50))
                == "transition");

        // The part type is trusted over the geometry: a reducer whose
        // pipes read as very slightly off-axis must not become an elbow,
        // which would put a right angle in a straight run.
        var nearlyStraight = Axes(East, new XYZ(-0.9995, 0.0316, 0));

        Check("a reducer whose pipes are a shade off-axis stays a "
              + "reducer, not an elbow",
            Make(Kind.Transition, nearlyStraight, Sizes(80, 50))
                == "transition");

        Check("but an unknown family that far off-axis IS an elbow",
            Make(Kind.Undefined, nearlyStraight, Sizes(50, 50))
                == "elbow");

        // --- a tee ---------------------------------------------------

        Check("a tee takes the odd pipe as its branch",
            Make(Kind.Tee, Axes(East, West, North), Sizes(80, 80, 50))
                == "tee branch=2");

        Check("and finds it wherever it comes in the list",
            Make(Kind.Tee, Axes(North, East, West), Sizes(50, 80, 80))
                == "tee branch=0");

        Check("a tee whose main is not quite straight still reads right",
            Make(Kind.Tee,
                    Axes(East, new XYZ(-0.999, 0.044, 0), South),
                    Sizes(80, 80, 50))
                == "tee branch=2");

        Check("a 45 degree lateral still branches off the main",
            Make(Kind.Tee,
                    Axes(East, West, new XYZ(0.707, 0.707, 0)),
                    Sizes(80, 80, 50))
                == "tee branch=2");

        Check("a tee turned on its side branches upwards",
            Make(Kind.Tee, Axes(North, South, Up), Sizes(65, 65, 40))
                == "tee branch=2");

        // --- a cross -------------------------------------------------

        Check("a cross pairs each pipe with the one opposite it",
            Make(Kind.Cross, Axes(East, North, West, South),
                    Sizes(80, 50, 80, 50))
                == "cross 0+2 1+3");

        // --- anything else -------------------------------------------

        Check("a fitting with five ends is refused rather than guessed at",
            Make(Kind.Undefined,
                    Axes(East, West, North, South, Up),
                    Sizes(80, 80, 50, 50, 50))
                == "refuse");

        Check("and so is one with a single end",
            Make(Kind.Undefined, Axes(East), Sizes(80)) == "refuse");

        // --- does it agree with its pipe? -----------------------------

        Check("half a millimetre out counts as the same size",
            W(100.0, 100.4).Agrees);

        Check("a millimetre out does not",
            !W(100.0, 101.0).Agrees);

        Check("DN50 against DN65 certainly does not",
            !W(50, 65).Agrees);

        Console.WriteLine();
        Console.WriteLine(_failed == 0
            ? "all checks passed"
            : _failed + " check(s) FAILED");

        return _failed == 0 ? 0 : 1;
    }
}
