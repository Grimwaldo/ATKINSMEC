using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // WHICH FITTING A SHAPE NEEDS - the Revit-free half of QACHWFITTINGS'
    // replace step: which of Revit's creation calls a replaced fitting
    // needs, and which pipe of a tee is its branch.
    //
    // This is the part that can be got wrong SILENTLY. A tee rebuilt with
    // the wrong pipe as its branch still comes out a tee, still connects,
    // and is wrong on the drawing; nothing else would catch it. It used to
    // live inside QACHWFITTINGS on Revit's XYZ and PartType, so the only
    // test was a hand-kept COPY (tests\fittingtest) - and the copy fell
    // behind: it still said "straight" at 0.999 after the add-in moved to
    // Revit's own 2 degrees. Moved here unchanged on 2026-10-06 so the
    // test compiles the real thing. QAVEC is QACHWALIGN's Revit-free
    // vector.
    public static class QACHWSHAPE
    {
        // Two ends this nearly in line are STRAIGHT: cos(2 degrees). The 2
        // degrees is Revit's own - NewElbowFitting takes a turn of
        // "typically 2 to 95 degrees" - so below it the answer has to be
        // a union or a reducer.
        public const double Straight = 0.9994;

        // Two sizes this close are the same size
        public const double AgreeMm = 0.5;

        // What a family CALLS itself (MechanicalFitting.PartType), as far
        // as the choice cares; anything else is Nothing
        public enum Says { Nothing, Elbow, Transition, Union }

        public enum Two { Elbow, Union, Transition }

        public static bool SameSize(double aMm, double bMm)
        {
            return Math.Abs(aMm - bMm) <= AgreeMm;
        }

        // Putting a two-ended fitting between two pipe ends: an elbow
        // where they turn, a union where they meet in line at the same
        // size, a reducer where they meet in line at different sizes.
        // The part type is the better answer where there is one; the
        // geometry is the fallback for a family that will not say what it
        // is. dot is between the two ends' facing directions.
        public static Two Join(Says says, double dot, bool sameSize)
        {
            bool straight = Math.Abs(dot) > Straight;

            if (says == Says.Elbow
                || (says != Says.Transition
                    && says != Says.Union
                    && !straight))
                return Two.Elbow;

            return sameSize ? Two.Union : Two.Transition;
        }

        // Of three pipes into a tee, the branch is the one that is not
        // in line with the other two
        public static int Odd(IList<QAVEC> axes)
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

        // Which of the others is most nearly in line with this one - a
        // cross is two such pairs
        public static int Partner(IList<QAVEC> axes, int of)
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
    }
}
