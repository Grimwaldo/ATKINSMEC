using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // The two decisions behind reconnecting a CHW pipe to a fan coil,
    // both written with no Revit in them and tested at
    // scratchpad/chwjointtest:
    //
    //   1. WHICH connector does this pipe belong to?  Supply to supply,
    //      return to return - never the nearest one regardless.
    //   2. CAN the pipe be brought onto it, and how?
    //
    // Getting the first wrong swaps flow and return, which looks
    // perfectly correct on the drawing and is wrong in every
    // calculation afterwards. That is why it is a rule rather than a
    // distance.

    // A vector, so the geometry can be reasoned about and tested
    // without Revit. Everything is in FEET, Revit's internal unit.
    public struct QAVEC
    {
        public double X;
        public double Y;
        public double Z;

        public QAVEC(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Length
        {
            get { return Math.Sqrt(X * X + Y * Y + Z * Z); }
        }

        public QAVEC Minus(QAVEC other)
        {
            return new QAVEC(X - other.X, Y - other.Y, Z - other.Z);
        }

        public QAVEC Times(double by)
        {
            return new QAVEC(X * by, Y * by, Z * by);
        }

        public double Dot(QAVEC other)
        {
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        public QAVEC Unit()
        {
            double length = Length;

            return length < 1e-12
                ? new QAVEC(0, 0, 0)
                : new QAVEC(X / length, Y / length, Z / length);
        }

        public override string ToString()
        {
            return "(" + X.ToString("0.###") + ", " +
                   Y.ToString("0.###") + ", " +
                   Z.ToString("0.###") + ")";
        }
    }

    // What a pipe or a connector carries. The int is Revit's own
    // PipeSystemType, kept as an int so this file needs no Revit; 0 is
    // its UndefinedSystemType.
    public static class QACHWSIDE
    {
        public const int Undefined = 0;

        // NO PipeSystemType VALUES ARE NAMED HERE. This file is
        // Revit-free, and writing Revit's enum numbers into it would be
        // an assumption nothing could check - a reference assembly
        // cannot be executed to read them back. Everything here
        // compares ints for EQUALITY, which needs no constants;
        // QACHWJOINTS asks the enum itself, because it is the only
        // place that can see it.

        // Does this pipe belong on that connector?
        //
        // Both say what they are, and they must say the SAME thing.
        public static bool Match(int pipeSystem, int connectorSystem)
        {
            return Match(pipeSystem, connectorSystem, false);
        }

        // WITH allowUnknown, A SIDE THAT SAYS NOTHING NO LONGER REFUSES
        // - and this does NOT relax the rule that matters.
        //
        // Two sides that BOTH say what they are must still say the
        // same thing, so supply never reaches return. It also means a
        // refrigerant pipe still cannot reach the CONDENSATE DRAIN
        // connector beside it: Sanitary and SupplyHydronic are both
        // known and different, so that stays refused exactly as before.
        //
        // What it relaxes is the case where REVIT ITSELF HAS NO
        // OPINION. A connector at UndefinedSystemType is not a return
        // connector being mistaken for a supply one - it is a connector
        // nobody classified, so there is no information there to
        // protect and refusing it protects nothing.
        //
        // Manufacturer VRV, VRF and split families ship like this as a
        // matter of course. A real model gave 233 free equipment
        // connectors carrying "no system" against 127 SupplyHydronic
        // and 125 ReturnHydronic pipe ends - with pipe ends sitting
        // 0 mm off a connector and being turned away. USER-REPORTED
        // twice, and the scan log is what proved it.
        //
        // Which of two unclassified connectors a pipe takes is then
        // settled by the description and the distance, exactly as it
        // already is for a four-pipe unit whose connectors both say
        // SupplyHydronic - and Choice.Ambiguous still says so when only
        // the distance chose.
        public static bool Match(
            int pipeSystem, int connectorSystem, bool allowUnknown)
        {
            if (pipeSystem == Undefined || connectorSystem == Undefined)
                return allowUnknown;

            return pipeSystem == connectorSystem;
        }
    }

    // How a pipe end can be brought onto a connector.
    //
    // The gap between them is split into what the PIPE ITSELF can
    // absorb by getting longer, and what the pipe it hangs off can
    // absorb by the fitting between them sliding along. Anything left
    // over after those two is a direction nothing can give, and the
    // whole thing is refused rather than bent.
    public class QACHWALIGN
    {
        public enum Kind
        {
            // The ends already meet: just join them
            Meet = 0,

            // Only the pipe's own length has to change
            Stretch,

            // The pipe is moved BODILY onto the connector's line - to
            // its elevation, usually - and then stretched onto it.
            // Only possible when its far end is free, because nothing
            // else can come with it.
            Shift,

            // The fitting above has to slide along its pipe as well
            Slide,

            // Nothing honest can be done
            Refuse
        }

        public class Plan
        {
            public Kind What = Kind.Refuse;

            // How much longer the pipe becomes, along its own axis.
            // Negative means shorter.
            public double AlongPipe;

            // How far the fitting above slides, along the neighbouring
            // pipe's axis
            public double AlongNeighbour;

            // Which way, and how far, the whole pipe moves for a Shift.
            // Perpendicular to its own axis by construction.
            public QAVEC Across;

            // What neither could absorb. Must be inside the tolerance
            // or the plan is a refusal.
            public double LeftOver;

            public string Why = string.Empty;
        }

        // Two ends within this are already touching, near enough
        public const double Touching = 0.0016;   // 0.5 mm in feet

        // Anything smaller than this is not a real movement
        public const double Nothing = 1e-7;

        // The gap is FROM the pipe's end TO the connector.
        //
        // pipeAxis points along the pipe, outward from its far end
        // towards the end being moved. neighbourAxis is the pipe the
        // fitting above sits on, or a zero vector when there is none.
        //
        // farEndFree says whether the pipe's OTHER end is joined to
        // anything. When it is not, the whole pipe can simply be moved
        // onto the connector's line - which is what "put it at the same
        // elevation as the connector" means, and the commonest thing a
        // person does by hand. Nothing else moves, because nothing else
        // is attached.
        public static Plan Make(
            QAVEC gap,
            QAVEC pipeAxis,
            QAVEC neighbourAxis,
            bool farEndFree,
            double tolerance)
        {
            var plan = new Plan();

            double distance = gap.Length;

            if (distance <= Touching)
            {
                plan.What = Kind.Meet;
                plan.Why = "the ends already meet";

                return plan;
            }

            QAVEC along = pipeAxis.Unit();

            if (along.Length < 0.5)
            {
                plan.Why = "the pipe has no direction to read";

                return plan;
            }

            // How much of the gap lies along the pipe itself
            plan.AlongPipe = gap.Dot(along);

            QAVEC rest = gap.Minus(along.Times(plan.AlongPipe));

            if (rest.Length <= tolerance)
            {
                plan.What = Kind.Stretch;
                plan.LeftOver = rest.Length;
                plan.Why = "the connector is on the pipe's own line";

                return plan;
            }

            // A PIPE LOOSE AT BOTH ENDS JUST MOVES. No joint has to
            // survive it and no other element is touched, so this is
            // both the simplest answer and the safest - and it is the
            // only one that can absorb an offset in a direction nothing
            // in the model runs in, a pipe simply drawn at the wrong
            // elevation.
            if (farEndFree)
            {
                plan.What = Kind.Shift;
                plan.Across = rest;
                plan.LeftOver = 0;
                plan.Why =
                    "the pipe is free at its far end, so it moves onto " +
                    "the connector's line";

                return plan;
            }

            // What is left has to come from the pipe above, by the
            // fitting between them sliding along it
            QAVEC across = neighbourAxis.Unit();

            if (across.Length < 0.5)
            {
                plan.LeftOver = rest.Length;
                plan.Why =
                    "the connector is off the pipe's line and there is " +
                    "nothing above it to slide along";

                return plan;
            }

            plan.AlongNeighbour = rest.Dot(across);

            QAVEC leftOver = rest.Minus(across.Times(plan.AlongNeighbour));

            plan.LeftOver = leftOver.Length;

            if (plan.LeftOver > tolerance)
            {
                plan.Why =
                    "the connector is off in a direction neither the " +
                    "pipe nor the one above it runs in";

                return plan;
            }

            plan.What = Kind.Slide;
            plan.Why = "the fitting above slides along and the pipe follows";

            return plan;
        }

        // One connector a pipe might belong on
        public class Candidate
        {
            // Revit's PipeSystemType, as an int
            public int System;

            public double Distance;

            // How much this connector's DESCRIPTION looks like the
            // pipe's system name. Higher is better; zero means nothing
            // is known either way.
            public int Hint;
        }

        public class Choice
        {
            public int At = -1;

            // More than one connector of the right system was on
            // offer and nothing but distance separated them. The
            // answer is probably right and is worth saying out loud.
            public bool Ambiguous;

            public bool Found
            {
                get { return At >= 0; }
            }
        }

        // Which connector, of those a unit offers, this pipe belongs on.
        //
        // THE SYSTEM DECIDES FIRST, always. Then the connector's
        // description, then the distance. Each connector is claimed
        // once - the greedy one-to-one matching ALIGNTERMINALS uses,
        // for the same reason: two pipes must not both be sent to the
        // same connector.
        //
        // The description matters on a FOUR-PIPE unit. Revit gives a
        // free connector only its CLASSIFICATION - SupplyHydronic or
        // ReturnHydronic - and a fan coil with a chilled coil and a
        // heating coil has TWO connectors that both say SupplyHydronic.
        // Nothing in the API distinguishes them, so a chilled water
        // pipe could be sent to the heating connector on distance
        // alone. Where the family author described them, that is the
        // only thing that can tell them apart.
        public static Choice Pick(
            int pipeSystem,
            IList<Candidate> among,
            ICollection<int> taken)
        {
            return Pick(pipeSystem, among, taken, false);
        }

        public static Choice Pick(
            int pipeSystem,
            IList<Candidate> among,
            ICollection<int> taken,
            bool allowUnknown)
        {
            var choice = new Choice();

            int best = -1;
            int bestHint = -1;

            double closest = double.MaxValue;

            int rightSystem = 0;

            for (int i = 0; i < among.Count; i++)
            {
                if (taken != null && taken.Contains(i))
                    continue;

                if (!QACHWSIDE.Match(
                        pipeSystem, among[i].System, allowUnknown))
                    continue;

                rightSystem++;

                bool better =
                    among[i].Hint > bestHint ||
                    (among[i].Hint == bestHint &&
                     among[i].Distance < closest);

                if (!better)
                    continue;

                bestHint = among[i].Hint;
                closest = among[i].Distance;
                best = i;
            }

            if (best < 0)
                return choice;

            choice.At = best;

            // Ambiguous only when the description could not separate
            // them: with a hint, the choice was made on something real
            choice.Ambiguous = rightSystem > 1 && bestHint <= 0;

            if (taken != null)
                taken.Add(best);

            return choice;
        }

        // How much a connector's description looks like a pipe's system
        // name. Words of three letters or more, letters only, compared
        // without case - so "CHW Supply" and "Chilled Water Supply"
        // share "supply", and "HHW Supply" shares it too but nothing
        // else, which is exactly the separation wanted on a four-pipe
        // unit where both connectors say SupplyHydronic.
        public static int Hint(string description, string systemName)
        {
            List<string> mine = Words(description);
            List<string> theirs = Words(systemName);

            if (mine.Count == 0 || theirs.Count == 0)
                return 0;

            int shared = 0;

            foreach (string word in mine)
            {
                if (theirs.Contains(word))
                    shared++;
            }

            return shared;
        }

        private static List<string> Words(string text)
        {
            var words = new List<string>();

            if (string.IsNullOrEmpty(text))
                return words;

            var word = new System.Text.StringBuilder();

            foreach (char letter in text)
            {
                if (char.IsLetter(letter))
                {
                    word.Append(char.ToLowerInvariant(letter));

                    continue;
                }

                Keep(words, word);
            }

            Keep(words, word);

            return words;
        }

        private static void Keep(
            List<string> words, System.Text.StringBuilder word)
        {
            // Two letters is "of", "to", "in" - noise. Three is the
            // shortest thing worth matching on: CHW, HHW, LTHW.
            if (word.Length >= 3)
            {
                string made = word.ToString();

                if (!words.Contains(made))
                    words.Add(made);
            }

            word.Length = 0;
        }
    }
}
