using ATKINSMEC;
using System;
using System.Collections.Generic;

// The decisions in QACHWFITTINGS' replace step - which of Revit's five
// creation calls a replaced fitting needs, and which pipe is the branch
// of a tee - with no Revit present. The REAL rules are compiled in
// (QACHWSHAPE); only the dispatch on how many ends a fitting has is
// repeated here. Until 2026-10-06 this was a hand-kept copy, and it had
// fallen behind the add-in's 2-degree rule for straight.
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

    private const double AgreeMm = QACHWSHAPE.AgreeMm;

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
    // The real decisions (QACHWSHAPE), behind the same small adapter
    // QACHWFITTINGS.Make is: two ends, three, or four
    // ==================================================

    private static QACHWSHAPE.Says SaysOf(Kind kind)
    {
        switch (kind)
        {
            case Kind.Elbow: return QACHWSHAPE.Says.Elbow;
            case Kind.Transition: return QACHWSHAPE.Says.Transition;
            case Kind.Union: return QACHWSHAPE.Says.Union;
            default: return QACHWSHAPE.Says.Nothing;
        }
    }

    private static string Make(
        Kind kind, List<XYZ> axes, List<Want> wants)
    {
        List<QAVEC> v = axes.ConvertAll(a => new QAVEC(a.X, a.Y, a.Z));

        if (v.Count == 2)
        {
            switch (QACHWSHAPE.Join(
                        SaysOf(kind), v[0].Dot(v[1]),
                        QACHWSHAPE.SameSize(wants[0].WantMm, wants[1].WantMm)))
            {
                case QACHWSHAPE.Two.Elbow: return "elbow";
                case QACHWSHAPE.Two.Union: return "union";
                default: return "transition";
            }
        }

        if (v.Count == 3)
            return "tee branch=" + QACHWSHAPE.Odd(v);

        if (v.Count == 4)
        {
            int partner = QACHWSHAPE.Partner(v, 0);

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

        // THE 2-DEGREE LINE IS REVIT'S. This check used to say an unknown
        // family 1.8 degrees off-axis IS an elbow - written against an
        // older, tighter "straight". NewElbowFitting will not make a turn
        // under about 2 degrees, so the add-in moved to cos(2 deg); below
        // it the answer has to be a union or a reducer, and an elbow
        // asked for there would only be refused.
        var bent = Axes(East, new XYZ(-Math.Cos(5 * Math.PI / 180), Math.Sin(5 * Math.PI / 180), 0));

        Check("an unknown family that really turns (5 degrees) IS an elbow",
            Make(Kind.Undefined, bent, Sizes(50, 50)) == "elbow");

        Check("an unknown family 1.8 degrees off-axis is straight - a union, "
              + "since Revit makes no elbow that shallow",
            Make(Kind.Undefined, nearlyStraight, Sizes(50, 50)) == "union");

        XYZ Off(double degrees)
        {
            double r = degrees * Math.PI / 180;
            return new XYZ(-Math.Cos(r), Math.Sin(r), 0);
        }

        Check("the line is at 2 degrees: 1.9 is straight, 2.1 is a turn",
            Make(Kind.Undefined, Axes(East, Off(1.9)), Sizes(50, 50)) == "union" &&
            Make(Kind.Undefined, Axes(East, Off(2.1)), Sizes(50, 50)) == "elbow");

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
