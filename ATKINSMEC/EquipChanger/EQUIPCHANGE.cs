using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Swaps a piece of mechanical equipment (or an air terminal) for
    // another type and brings the ductwork on it along.
    //
    // THE ORDER IS THE WHOLE POINT. Revit raises
    //
    //     "The family is connected to a network and can no longer keep
    //      the connectivity. Disconnect the family from the network?"
    //
    // as an error that CANNOT BE IGNORED - user-reported, four of them
    // at once - because the type is swapped while the family is still
    // joined to ducts it no longer fits. So the joints come apart
    // first, on purpose and under this add-in's control, and are put
    // back afterwards once everything is the right size and in the
    // right place. Nothing is ever asked to "keep connectivity" through
    // a change it cannot survive.
    public static class EQUIPCHANGE
    {
        public class Outcome
        {
            public long Id;

            public string Name = string.Empty;
            public string WasType = string.Empty;
            public string NowType = string.Empty;

            public bool Changed;

            // It was already the type that was asked for
            public bool AlreadyThat;

            // How many ducts, fittings and accessories followed
            public int Resized;

            // One line per connector: where its branch stopped
            public List<string> Branches = new List<string>();

            public string Said = string.Empty;
        }

        public class Result
        {
            public List<Outcome> Outcomes = new List<Outcome>();

            public int Changed;
            public int Refused;

            // Changing a unit to the type it already has does nothing
            // and looks exactly like a broken button. Counted on its
            // own so the window can say so.
            public int Already;

            // Set when nothing could be attempted at all
            public string Blocked = string.Empty;
        }

        // ==================================================
        // What WOULD happen - reads only
        // ==================================================

        // Walked before anything is touched, so the window can say what
        // it is about to do and somebody can disagree with it. Resizing
        // ductwork is not a thing to do blind.
        public static Outcome Preview(
            Document doc, Element source, FamilySymbol wanted)
        {
            var outcome = new Outcome
            {
                Id = source.Id.ToValue(),
                Name = Named(source),
                WasType = TypeNameOf(source),
                NowType = wanted == null ? string.Empty : wanted.Name
            };

            List<Connector> ends = EQUIPWALK.EndsOf(source);

            if (ends.Count == 0)
            {
                outcome.Said = "no duct connectors on it";

                return outcome;
            }

            int index = 0;

            foreach (Connector end in ends)
            {
                index++;

                EQUIPWALK.Branch branch =
                    EQUIPWALK.From(doc, end, index);

                outcome.Resized += branch.Ids.Count;

                outcome.Branches.Add(
                    "connector " + index + ": " + branch.Ids.Count +
                    " element(s), " + branch.Said);
            }

            return outcome;
        }

        // ==================================================
        // Doing it
        // ==================================================

        public static Result Run(
            Document doc,
            List<long> sourceIds,
            long symbolId,
            bool resizeDucts)
        {
            var result = new Result();

            if (doc == null || sourceIds == null || sourceIds.Count == 0)
            {
                result.Blocked = "Nothing was selected to change.";

                return result;
            }

            var symbol = doc.GetElement(
                REVITCOMPAT.NewId(symbolId)) as FamilySymbol;

            if (symbol == null)
            {
                result.Blocked = "That type could not be found.";

                return result;
            }

            EQUIPLOG.Start("Changing " + sourceIds.Count + " element(s) to " +
                           symbol.Name);

            using (var transaction = new Transaction(doc, "Change equipment type"))
            {
                transaction.Start();

                // An error Revit calls "cannot be ignored" would
                // otherwise put a modal dialog in front of somebody in
                // the middle of a batch. This turns it into a rollback
                // with a reason, which is what a caller can act on.
                var quiet = new EQUIPFAILURES();

                FailureHandlingOptions options =
                    transaction.GetFailureHandlingOptions();

                options.SetFailuresPreprocessor(quiet);
                options.SetClearAfterRollback(true);

                transaction.SetFailureHandlingOptions(options);

                if (!symbol.IsActive)
                {
                    symbol.Activate();

                    doc.Regenerate();
                }

                foreach (long id in sourceIds)
                {
                    Element source = doc.GetElement(REVITCOMPAT.NewId(id));

                    if (source == null)
                        continue;

                    Outcome outcome = One(
                        doc, source, symbol, resizeDucts, quiet);

                    result.Outcomes.Add(outcome);

                    if (outcome.Changed)
                        result.Changed++;
                    else if (outcome.AlreadyThat)
                        result.Already++;
                    else
                        result.Refused++;
                }

                transaction.Commit();

                // REVIT PROCESSES FAILURES WHEN THE TRANSACTION
                // COMMITS, not when each SubTransaction does. So
                // everything above ran with quiet.Errored still false,
                // and a preprocessor answering ProceedWithRollBack at
                // the very end takes the WHOLE RUN with it - every
                // element, including the ones that were fine.
                //
                // Which means the outcomes gathered above are what was
                // ATTEMPTED, not what happened. The only honest report
                // is to ask the model, after the commit, what it
                // actually has. Anything else claims success for a
                // change that was rolled back underneath it.
                Verify(doc, symbolId, result, quiet);
            }

            EQUIPLOG.Head("Done");
            EQUIPLOG.Say("   " + result.Changed + " changed, " +
                         result.Refused + " refused");

            EQUIPLOG.Save();

            return result;
        }

        // What the model really has now
        private static void Verify(
            Document doc, long symbolId, Result result, EQUIPFAILURES quiet)
        {
            int lost = 0;

            foreach (Outcome outcome in result.Outcomes)
            {
                if (!outcome.Changed)
                    continue;

                var instance = doc.GetElement(
                    REVITCOMPAT.NewId(outcome.Id)) as FamilyInstance;

                bool really = false;

                try
                {
                    really = instance != null &&
                             instance.Symbol != null &&
                             instance.Symbol.Id.ToValue() == symbolId;
                }
                catch
                {
                }

                if (really)
                    continue;

                outcome.Changed = false;

                outcome.Said = string.IsNullOrEmpty(quiet.Said)
                    ? "Revit put it back when the change was committed"
                    : "Revit put it back when the change was committed: " +
                      quiet.Said;

                result.Changed--;
                result.Refused++;

                lost++;
            }

            if (lost > 0)
            {
                EQUIPLOG.Head("Rolled back at the commit");
                EQUIPLOG.Say("   " + lost + " element(s) did not survive");
                EQUIPLOG.Say("   " + quiet.Said);
            }
        }

        private static Outcome One(
            Document doc,
            Element source,
            FamilySymbol symbol,
            bool resizeDucts,
            EQUIPFAILURES quiet)
        {
            var outcome = new Outcome
            {
                Id = source.Id.ToValue(),
                Name = Named(source),
                WasType = TypeNameOf(source),
                NowType = symbol.Name
            };

            EQUIPLOG.Head(outcome.Name + "  (" + outcome.WasType + " -> " +
                          symbol.Name + ")");

            var instance = source as FamilyInstance;

            if (instance == null)
            {
                outcome.Said = "not a family instance";

                EQUIPLOG.Say("   refused: " + outcome.Said);

                return outcome;
            }

            // ALREADY THAT TYPE. Setting Symbol to what it already is
            // succeeds, changes nothing and reports success - which
            // from outside is indistinguishable from a button that does
            // not work. User-reported as "it doesn't change the family
            // type", and this was very probably it: a family with one
            // type offers only the type the unit already has.
            try
            {
                if (instance.Symbol != null &&
                    instance.Symbol.Id.ToValue() == symbol.Id.ToValue())
                {
                    outcome.AlreadyThat = true;

                    outcome.Said =
                        "it is already " + symbol.Name + " - nothing to do";

                    EQUIPLOG.Say("   " + outcome.Said);

                    return outcome;
                }
            }
            catch
            {
            }

            // Everything is one SubTransaction, so a refusal takes this
            // one element back to exactly where it was and the rest of
            // the run still goes through - QADUCTFIX's rule.
            using (var step = new SubTransaction(doc))
            {
                step.Start();

                quiet.Clear();

                try
                {
                    if (Change(doc, instance, symbol, resizeDucts, outcome))
                    {
                        if (quiet.Errored)
                        {
                            outcome.Changed = false;

                            outcome.Said = "Revit refused it: " + quiet.Said;

                            EQUIPLOG.Say("   rolled back - " + outcome.Said);

                            step.RollBack();

                            return outcome;
                        }

                        step.Commit();

                        outcome.Changed = true;

                        EQUIPLOG.Say("   changed, " + outcome.Resized +
                                     " element(s) resized");

                        return outcome;
                    }

                    step.RollBack();

                    EQUIPLOG.Say("   rolled back - " + outcome.Said);

                    return outcome;
                }
                catch (Exception ex)
                {
                    step.RollBack();

                    outcome.Changed = false;
                    outcome.Said = Short(ex);

                    EQUIPLOG.Blew("changing " + outcome.Name, ex);

                    return outcome;
                }
            }
        }

        // The five steps, in the one order that works
        private static bool Change(
            Document doc,
            FamilyInstance instance,
            FamilySymbol symbol,
            bool resizeDucts,
            Outcome outcome)
        {
            // ---- 1. what is on it, and what follows -----------------
            List<Connector> ends = EQUIPWALK.EndsOf(instance);

            if (ends.Count == 0)
            {
                outcome.Said = "it has no duct connectors";

                return false;
            }

            var branches = new List<EQUIPWALK.Branch>();

            var joins = new List<EQUIPJOINT>();

            int index = 0;

            foreach (Connector end in ends)
            {
                index++;

                EQUIPWALK.Branch branch = EQUIPWALK.From(doc, end, index);

                branches.Add(branch);

                outcome.Branches.Add(
                    "connector " + index + ": " + branch.Ids.Count +
                    " element(s), " + branch.Said);

                EQUIPLOG.Say("   connector " + index + ": " +
                             branch.Ids.Count + " element(s), " +
                             branch.Said);

                if (branch.Stopped == EQUIPSTOPKIND.Unknown &&
                    branch.Ids.Count == 0)
                {
                    // Refusing here is deliberate: a branch this cannot
                    // read is one it must not resize
                    outcome.Said = "connector " + index + " " + branch.Said;

                    return false;
                }

                // What this connector is joined to now, so it can be
                // put back exactly
                EQUIPJOINT joint = EQUIPJOINT.Of(end, index);

                if (joint != null)
                    joins.Add(joint);
            }

            // ---- 2. come apart, on purpose --------------------------
            //
            // This is what stops Revit raising "cannot keep the
            // connectivity": it is no longer being asked to.
            foreach (EQUIPJOINT joint in joins)
                joint.Undo(doc);

            doc.Regenerate();

            // ---- 3. the type itself ---------------------------------
            try
            {
                instance.Symbol = symbol;
            }
            catch (Exception ex)
            {
                outcome.Said = "the type would not go on: " + Short(ex);

                return false;
            }

            doc.Regenerate();

            // ---- 4. the ductwork follows ----------------------------
            if (resizeDucts)
            {
                List<Connector> now = EQUIPWALK.EndsOf(instance);

                for (int i = 0; i < branches.Count; i++)
                {
                    if (i >= now.Count)
                        break;

                    EQUIPSIZE wanted = EQUIPWALK.SizeOf(now[i]);

                    if (!wanted.Known)
                    {
                        EQUIPLOG.Say("   connector " + (i + 1) +
                                     ": the new type will not say its size");

                        continue;
                    }

                    EQUIPLOG.Say("   connector " + (i + 1) + " is now " +
                                 wanted);

                    outcome.Resized += EQUIPSIZER.Drive(
                        doc, branches[i].Ids, wanted);
                }

                doc.Regenerate();
            }

            // ---- 5. back together -----------------------------------
            List<Connector> after = EQUIPWALK.EndsOf(instance);

            var taken = new HashSet<int>();

            foreach (EQUIPJOINT joint in joins)
            {
                Connector mine = EQUIPJOINT.Nearest(joint, after, taken);

                if (mine == null)
                {
                    outcome.Said =
                        "the new type has no connector left for " +
                        "connector " + joint.Index;

                    return false;
                }

                if (!joint.Redo(doc, mine))
                {
                    outcome.Said =
                        "connector " + joint.Index +
                        " could not be joined back up";

                    return false;
                }
            }

            doc.Regenerate();

            return true;
        }

        // ==================================================

        public static string Named(Element element)
        {
            if (element == null)
                return string.Empty;

            try
            {
                Parameter mark = element.get_Parameter(
                    BuiltInParameter.ALL_MODEL_MARK);

                string said = mark == null
                    ? string.Empty
                    : (mark.AsString() ?? string.Empty);

                return said.Length > 0
                    ? said
                    : element.Name + " " + element.Id.ToValue();
            }
            catch
            {
                return element.Id.ToValue().ToString();
            }
        }

        public static string TypeNameOf(Element element)
        {
            try
            {
                var instance = element as FamilyInstance;

                if (instance != null && instance.Symbol != null)
                    return instance.Symbol.Name;
            }
            catch
            {
            }

            return string.Empty;
        }

        public static string Short(Exception ex)
        {
            if (ex == null)
                return string.Empty;

            string said = ex.Message ?? string.Empty;

            said = said.Replace("\r", " ").Replace("\n", " ").Trim();

            return said.Length > 220 ? said.Substring(0, 220) + "..." : said;
        }
    }
}
