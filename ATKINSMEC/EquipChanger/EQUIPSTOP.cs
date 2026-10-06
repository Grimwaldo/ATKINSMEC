using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Where a resize STOPS, and why.
    //
    // The whole add-in turns on this one decision, so it is written
    // here with no Revit in it and tested directly at
    // scratchpad/equiptest. The walk in EQUIPWALK does nothing but
    // describe each element it meets and ask.
    public enum EQUIPSTOPKIND
    {
        // Keep going, and resize this one
        Cross = 0,

        // A reducer exists BECAUSE the size changes there. Going past
        // it would undo somebody's design.
        Reducer,

        // A tap, spud or takeoff: the branch is served from a main
        // that is nothing to do with this equipment
        Tap,

        // A tee or a cross - the flow divides
        Branch,

        // An air terminal at the far end
        Terminal,

        // Other equipment
        Equipment,

        // A fitting that will not say what it is, or has an odd number
        // of ends: refused rather than guessed at
        Unknown
    }

    // What the walk saw, with no Revit types in it
    public class EQUIPNODE
    {
        public enum Sort
        {
            Duct = 0,
            Fitting,
            Accessory,
            Terminal,
            Equipment,
            Other
        }

        public Sort What { get; set; }

        // MechanicalFitting.PartType as plain text, empty when the
        // family will not say
        public string PartType { get; set; }

        // HVAC END connectors only
        public int Ends { get; set; }

        // Its two ends are different sizes, which makes it a reducer
        // whatever it calls itself
        public bool EndsDiffer { get; set; }

        public EQUIPNODE()
        {
            PartType = string.Empty;
        }
    }

    public static class EQUIPSTOP
    {
        // The part types that end a resize. Named rather than switched
        // on, so a family that DOES say what it is gets the right
        // answer even when its geometry is unusual.
        private static readonly string[] Reducers = { "Transition" };

        private static readonly string[] Taps =
        {
            "TapAdjustable", "TapPerpendicular",
            "SpudAdjustable", "SpudPerpendicular",
            "TapBurstProof", "Tap"
        };

        private static readonly string[] Branches =
        {
            "Tee", "Cross", "Wye", "Lateral", "TeeWye", "DoubleTee"
        };

        public static EQUIPSTOPKIND Decide(EQUIPNODE node)
        {
            if (node == null)
                return EQUIPSTOPKIND.Unknown;

            switch (node.What)
            {
                case EQUIPNODE.Sort.Duct:
                    return EQUIPSTOPKIND.Cross;

                case EQUIPNODE.Sort.Terminal:
                    return EQUIPSTOPKIND.Terminal;

                case EQUIPNODE.Sort.Equipment:
                    return EQUIPSTOPKIND.Equipment;

                case EQUIPNODE.Sort.Other:
                    return EQUIPSTOPKIND.Unknown;
            }

            // A fitting or an accessory. What it CALLS itself comes
            // first: a family that says Transition is a reducer even if
            // its two ends happen to measure the same today.
            if (Named(node.PartType, Reducers))
                return EQUIPSTOPKIND.Reducer;

            if (Named(node.PartType, Taps))
                return EQUIPSTOPKIND.Tap;

            if (Named(node.PartType, Branches))
                return EQUIPSTOPKIND.Branch;

            // Then the geometry, for the families that say nothing.
            //
            // A tap has ONE end connector of its own - its main side is
            // hosted on the duct's body and is not an end at all, which
            // is the same thing that made QADUCTFIX replace a hanging
            // tap rather than repair it.
            if (node.Ends <= 1)
                return EQUIPSTOPKIND.Tap;

            if (node.Ends > 2)
                return EQUIPSTOPKIND.Branch;

            // Exactly two ends of different sizes IS a reducer, whatever
            // the family calls itself
            if (node.EndsDiffer)
                return EQUIPSTOPKIND.Reducer;

            // Two equal ends: an elbow, a union, a damper. This is the
            // only thing worth crossing.
            return EQUIPSTOPKIND.Cross;
        }

        public static bool Crosses(EQUIPNODE node)
        {
            return Decide(node) == EQUIPSTOPKIND.Cross;
        }

        // What to write in the report when a walk ends
        public static string Why(EQUIPSTOPKIND kind)
        {
            switch (kind)
            {
                case EQUIPSTOPKIND.Reducer:
                    return "stopped at a reducer";
                case EQUIPSTOPKIND.Tap:
                    return "stopped at a tap-off";
                case EQUIPSTOPKIND.Branch:
                    return "stopped at a tee";
                case EQUIPSTOPKIND.Terminal:
                    return "stopped at an air terminal";
                case EQUIPSTOPKIND.Equipment:
                    return "stopped at other equipment";
                case EQUIPSTOPKIND.Unknown:
                    return "stopped at something it could not read";
                default:
                    return "ran to the end of the branch";
            }
        }

        private static bool Named(string partType, string[] among)
        {
            if (string.IsNullOrEmpty(partType))
                return false;

            foreach (string one in among)
            {
                if (string.Equals(one, partType,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    // Moving one end of a duct or pipe.
    //
    // REWRITING A LINE MUST KEEP ITS DIRECTION. Revit stores a duct's
    // geometry as a LocationCurve running from endpoint 0 to endpoint
    // 1, and its connectors belong to those ends. Build the new line
    // the other way round - far end first, moved end second - and the
    // curve is reversed, which Revit rejects with
    //
    //     "The duct/pipe has been modified to be in the opposite
    //      direction causing the connections to be invalid."
    //
    // USER-REPORTED, from the log: every step of an equipment change
    // succeeded and the whole transaction was then rolled back at the
    // commit by that one error. The same mistake is in QACHWROOM's
    // Restretch, where Sound catches it as "a pipe has turned" and
    // refuses the slide instead - so it reads as a route that never
    // works rather than as a flip.
    //
    // The rule is only "which endpoint is moving", but it is the whole
    // bug, so it is named and tested rather than written twice.
    public static class EQUIPLINE
    {
        // True when the end being moved is the line's START (endpoint
        // 0), in which case the new line is (moved, finish). False
        // means it is the finish, and the new line is (start, moved).
        public static bool MovingIsStart(
            double gapToStart, double gapToFinish)
        {
            return gapToStart <= gapToFinish;
        }
    }

    // One duct size. Revit-free so the rules above and the harness can
    // both hold it; everything is in FEET, Revit's own internal units.
    public class EQUIPSIZE
    {
        public bool Round { get; set; }

        public double Diameter { get; set; }

        public double Width { get; set; }
        public double Height { get; set; }

        // Half a millimetre in feet. Duct sizes are round numbers in
        // mm, so anything finer than this is noise from the conversion.
        public const double Tolerance = 0.0016;

        public bool Known
        {
            get
            {
                return Round
                    ? Diameter > 1e-9
                    : Width > 1e-9 && Height > 1e-9;
            }
        }

        public bool Same(EQUIPSIZE other)
        {
            if (other == null || Round != other.Round)
                return false;

            if (Round)
                return Math.Abs(Diameter - other.Diameter) < Tolerance;

            return Math.Abs(Width - other.Width) < Tolerance &&
                   Math.Abs(Height - other.Height) < Tolerance;
        }

        // Written the way a duct is spoken about, in mm
        public override string ToString()
        {
            if (!Known)
                return "(unknown)";

            return Round
                ? Mm(Diameter) + " dia"
                : Mm(Width) + " x " + Mm(Height);
        }

        private static string Mm(double feet)
        {
            return Math.Round(feet * 304.8).ToString("0");
        }
    }
}
