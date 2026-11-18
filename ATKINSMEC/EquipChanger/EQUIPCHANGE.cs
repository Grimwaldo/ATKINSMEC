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

            // A duct or pipe that could not reach its new connector
            // straight, so its joint was left open for a person - one
            // line each. The unit IS changed.
            public List<string> LeftOpen = new List<string>();

            // What was MOVED to reach the new connectors: the unit onto
            // its duct, pipework carried over - one line each
            public List<string> Notes = new List<string>();

            // A go that moved something was thrown out at the commit,
            // and whether that something included carried pipework
            public bool Tried;
            public bool Carried;

            public string Said = string.Empty;
        }

        public class Result
        {
            public List<Outcome> Outcomes = new List<Outcome>();

            public int Changed;
            public int Refused;

            // Changed, with a joint left open
            public int Open;

            // Changed, with the unit or pipework moved to reach
            public int Moved;

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
            List<Connector> pipes = EQUIPWALK.PipeEndsOf(source);

            if (ends.Count == 0 && pipes.Count == 0)
            {
                outcome.Said = "no duct or pipe connectors on it";

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

            index = 0;

            foreach (Connector pipe in pipes)
            {
                index++;

                EQUIPJOINT joint = EQUIPJOINT.Of(pipe, "pipe connector " + index);

                if (joint != null)
                    outcome.Branches.Add(joint.Called + ": taken off and put back");
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

            // ONE TRANSACTION PER UNIT, inside one group so the run is
            // still a single undo step.
            //
            // REVIT PROCESSES FAILURES WHEN A TRANSACTION COMMITS, not
            // when a SubTransaction does. With the whole run in one
            // transaction, a single unit Revit refused at the commit
            // took EVERY unit with it - the help's "one that Revit
            // refuses is put back and the rest still go through" was
            // not true. Each unit's own commit is where its failures
            // are read, so each answers for itself.
            using (var group = new TransactionGroup(doc, "Change equipment type"))
            {
                group.Start();

                if (!symbol.IsActive)
                {
                    using (var activate = new Transaction(doc, "Activate type"))
                    {
                        activate.Start();

                        symbol.Activate();

                        doc.Regenerate();

                        activate.Commit();
                    }
                }

                foreach (long id in sourceIds)
                {
                    Element source = doc.GetElement(REVITCOMPAT.NewId(id));

                    if (source == null)
                        continue;

                    Outcome outcome = One(doc, source, symbol, resizeDucts);

                    result.Outcomes.Add(outcome);

                    if (outcome.Changed)
                    {
                        result.Changed++;

                        if (outcome.LeftOpen.Count > 0)
                            result.Open++;

                        if (outcome.Notes.Count > 0)
                            result.Moved++;
                    }
                    else if (outcome.AlreadyThat)
                        result.Already++;
                    else
                        result.Refused++;
                }

                if (result.Changed > 0)
                    group.Assimilate();
                else
                    group.RollBack();
            }

            EQUIPLOG.Head("Done");
            EQUIPLOG.Say("   " + result.Changed + " changed (" + result.Open +
                         " with a connection left open), " +
                         result.Refused + " refused");

            EQUIPLOG.Save();

            return result;
        }

        // Is it really that type now? Asked of the model after the
        // commit: the outcome gathered before it is what was ATTEMPTED.
        private static bool IsNow(Document doc, long id, long symbolId)
        {
            try
            {
                var instance = doc.GetElement(
                    REVITCOMPAT.NewId(id)) as FamilyInstance;

                return instance != null &&
                       instance.Symbol != null &&
                       instance.Symbol.Id.ToValue() == symbolId;
            }
            catch
            {
                return false;
            }
        }

        private static Outcome One(
            Document doc,
            Element source,
            FamilySymbol symbol,
            bool resizeDucts)
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

            // FIRST moving what has to move - the unit onto its duct, the
            // pipework over to the new connections. If Revit throws THAT
            // out at the commit: again with the unit moved but no
            // pipework carried (so a carry Revit will not take does not
            // cost the duct its joint), and then again moving nothing -
            // the swap still goes through with what cannot reach left
            // open.
            int level = Everything;

            Pending pending;

            bool atCommit = Attempt(doc, instance, symbol, resizeDucts, outcome, level, out pending);

            string first = string.Empty;

            while (!outcome.Changed && atCommit && outcome.Tried && level > Nothing)
            {
                if (first.Length == 0)
                    first = outcome.Said;

                // Nothing was carried: the unit's own move was refused
                level = level == Everything && outcome.Carried
                    ? UnitOnly
                    : Nothing;

                EQUIPLOG.Say(level == UnitOnly
                    ? "   AGAIN, moving the unit but carrying no pipework"
                    : "   AGAIN, moving nothing - what cannot reach is left open");

                outcome.Branches.Clear();
                outcome.Said = string.Empty;

                atCommit = Attempt(doc, instance, symbol, resizeDucts, outcome, level, out pending);
            }

            if (outcome.Changed && first.Length > 0)
            {
                outcome.LeftOpen.Insert(0,
                    (level == UnitOnly
                        ? "no pipework was carried over"
                        : "nothing was moved to reach them") +
                    " - Revit refused that (" + first + ")");
            }

            if (!outcome.Changed || pending == null)
                return outcome;

            // ---- 5. back together, one joint at a time
            foreach (EQUIPJOINT joint in pending.Joins)
                Rejoin(doc, outcome, joint, pending);

            EQUIPLOG.Say("   changed, " + outcome.Resized +
                         " element(s) resized" +
                         (outcome.Notes.Count == 0
                             ? string.Empty
                             : ", " + outcome.Notes.Count + " move(s)") +
                         (outcome.LeftOpen.Count == 0
                             ? string.Empty
                             : ", " + outcome.LeftOpen.Count +
                               " connection(s) LEFT OPEN"));

            return outcome;
        }

        // How much a go may move to reach the new connectors
        private const int Nothing = 0;
        private const int UnitOnly = 1;
        private const int Everything = 2;

        // One go at it, in the unit's own transaction. True when it was
        // thrown out AT THE COMMIT, which a second go moving nothing may
        // get past.
        private static bool Attempt(
            Document doc,
            FamilyInstance instance,
            FamilySymbol symbol,
            bool resizeDucts,
            Outcome outcome,
            int level,
            out Pending pending)
        {
            pending = null;

            // The unit's own transaction, so a refusal - before the
            // commit or AT it - takes this one element back to exactly
            // where it was and the rest of the run still goes through
            using (var transaction = new Transaction(doc, "Change equipment type"))
            {
                // An error Revit calls "cannot be ignored" would
                // otherwise put a modal dialog in front of somebody in
                // the middle of a batch. This turns it into a rollback
                // with a reason, which is what a caller can act on.
                var quiet = new EQUIPFAILURES();

                transaction.Start();

                FailureHandlingOptions options =
                    transaction.GetFailureHandlingOptions();

                options.SetFailuresPreprocessor(quiet);
                options.SetClearAfterRollback(true);

                transaction.SetFailureHandlingOptions(options);

                bool done;

                try
                {
                    done = Change(doc, instance, symbol, resizeDucts, outcome, level, out pending);
                }
                catch (Exception ex)
                {
                    transaction.RollBack();

                    Undone(outcome, Short(ex));

                    EQUIPLOG.Blew("changing " + outcome.Name, ex);

                    return false;
                }

                if (!done)
                {
                    transaction.RollBack();

                    Undone(outcome, outcome.Said);

                    EQUIPLOG.Say("   rolled back - " + outcome.Said);

                    return false;
                }

                TransactionStatus status;

                try
                {
                    status = transaction.Commit();
                }
                catch (Exception ex)
                {
                    EQUIPLOG.Blew("committing " + outcome.Name, ex);

                    status = TransactionStatus.Error;
                }

                // Failures are read HERE, at the commit - so only now
                // is it known whether it happened. Asked of the model.
                if (status != TransactionStatus.Committed ||
                    !IsNow(doc, outcome.Id, symbol.Id.ToValue()))
                {
                    if (transaction.HasStarted() && !transaction.HasEnded())
                        transaction.RollBack();

                    Undone(outcome, string.IsNullOrEmpty(quiet.Said)
                        ? "Revit put it back when the change was committed"
                        : "Revit refused it: " + quiet.Said);

                    EQUIPLOG.Say("   rolled back at the commit - " + outcome.Said);

                    return true;
                }

                outcome.Changed = true;

                EQUIPLOG.Say("   the type is changed; joining it back up");

                return false;
            }
        }

        // Nothing that was attempted is there any more
        private static void Undone(Outcome outcome, string said)
        {
            // Whether this go MOVED anything is kept: it is what decides
            // that a second go, moving nothing, is worth it
            outcome.Tried = outcome.Notes.Count > 0;

            outcome.Carried = false;

            foreach (string note in outcome.Notes)
            {
                if (note.StartsWith("carried"))
                    outcome.Carried = true;
            }

            outcome.Changed = false;
            outcome.Resized = 0;
            outcome.LeftOpen.Clear();
            outcome.Notes.Clear();
            outcome.Said = said ?? string.Empty;
        }

        // The five steps, in the one order that works. `reach`: move the
        // unit onto its duct and carry pipework over where a straight
        // run cannot reach (off on the second go).
        private static bool Change(
            Document doc,
            FamilyInstance instance,
            FamilySymbol symbol,
            bool resizeDucts,
            Outcome outcome,
            int level,
            out Pending pending)
        {
            pending = null;

            // ---- 1. what is on it, and what follows -----------------
            List<Connector> ends = EQUIPWALK.EndsOf(instance);
            List<Connector> pipes = EQUIPWALK.PipeEndsOf(instance);

            if (ends.Count == 0 && pipes.Count == 0)
            {
                outcome.Said = "it has no duct or pipe connectors";

                return false;
            }

            Grouped(doc, instance);

            var branches = new List<EQUIPWALK.Branch>();

            // The connector each branch leaves from, by its own id
            var starts = new List<int>();

            var joins = new List<EQUIPJOINT>();

            int index = 0;

            foreach (Connector end in ends)
            {
                index++;

                EQUIPWALK.Branch branch = EQUIPWALK.From(doc, end, index);

                branches.Add(branch);

                starts.Add(end.Id);

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
                EQUIPJOINT joint = EQUIPJOINT.Of(end, "connector " + index);

                if (joint != null)
                    joins.Add(joint);
            }

            // THE PIPES TOO. Not resized - only taken off and put back.
            // Left on, Revit drags them after the new connectors and the
            // swap is thrown out at the commit (FCU-02 -> FCU-10).
            index = 0;

            bool reach = level >= Everything;

            // Open pipe ends already taken by a connector
            var claimed = new HashSet<string>();

            foreach (Connector pipe in pipes)
            {
                index++;

                EQUIPJOINT joint = EQUIPJOINT.Of(pipe, "pipe connector " + index);

                if (joint == null)
                {
                    // Not joined: a pipe that only touches it is taken as
                    // its joint (unit 164, 2026-11-18)
                    if (reach && level >= Everything)
                        joint = EQUIPJOINT.Loose(doc, pipe, "pipe connector " + index, claimed);

                    if (joint == null)
                        continue;

                    joins.Add(joint);

                    outcome.Notes.Add(joint.Called + ": " + joint.OtherName +
                                      " touched it but was never joined - joined now");

                    continue;
                }

                joins.Add(joint);

                outcome.Branches.Add(joint.Called + ": taken off and put back");

                EQUIPLOG.Say("   " + joint.Called + ": on " + joint.OtherName);
            }

            bool sameFamily = SameFamily(instance, symbol);

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

            // Which new connector each joint is now. The same connector
            // of the family when it is the same family - never the
            // nearest by position (EQUIPMATCH).
            var was = new List<EQUIPPORT>();

            foreach (EQUIPJOINT joint in joins)
                was.Add(joint.Port);

            var now = new List<EQUIPPORT>();

            foreach (Connector connector in EQUIPWALK.JointEndsOf(instance))
            {
                now.Add(new EQUIPPORT
                {
                    Id = connector.Id,
                    Domain = (int)connector.Domain,
                    System = EQUIPWALK.SystemOf(connector),
                    At = EQUIPWALK.V(connector.Origin)
                });
            }

            Dictionary<int, int> map = EQUIPMATCH.Assign(was, now, sameFamily);

            // ---- 4. the ductwork follows ----------------------------
            if (resizeDucts)
            {
                for (int i = 0; i < branches.Count; i++)
                {
                    if (branches[i].Ids.Count == 0)
                        continue;

                    int onto;

                    if (!map.TryGetValue(starts[i], out onto))
                    {
                        EQUIPLOG.Say("   connector " + (i + 1) +
                                     ": the new type has no connection " +
                                     "for it, not resized");

                        continue;
                    }

                    EQUIPSIZE wanted = EQUIPWALK.SizeOf(
                        QADUCTS.Lookup(doc, outcome.Id, onto));

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

            // ---- 4b. the unit onto its duct --------------------------
            //
            // User, 2026-11-18: FCU-10's supply spigot (1534 wide) came
            // out 327 mm to the side of FCU-02's (890), on a 42 mm stub
            // to a transition - no straight duct reaches that. So the
            // UNIT moves sideways until its spigot is on the duct's line
            // again, as it would be moved by hand. Only when the biggest
            // duct connection cannot reach as it is.
            if (level >= UnitOnly)
                OntoItsDuct(doc, instance, joins, map, outcome);

            doc.Regenerate();

            // ---- 5. back together: AFTER this commits, one joint at a
            // time (One -> Rejoin), each in its own transaction
            pending = new Pending
            {
                Joins = joins,
                Map = map,
                Carry = level >= Everything
            };

            return true;
        }

        // What step 5 needs, kept from the type swap's transaction
        private class Pending
        {
            public List<EQUIPJOINT> Joins = new List<EQUIPJOINT>();
            public Dictionary<int, int> Map = new Dictionary<int, int>();
            public bool Carry;
        }

        // ---- 5. back together -----------------------------------
        //
        // ONE JOINT, ITS OWN TRANSACTION (dry run, 2026-11-18: carrying
        // unit 89's supply duct took a tap off a duct, Revit threw the
        // commit out, and the pipes - which had nothing to do with it -
        // went with it). A duct or pipe that cannot reach its new
        // connector without bending or turning inside out is carried over
        // or offset when it can be (EQUIPJOINT.Redo) and otherwise LEFT
        // OPEN and said so. Refused AT THE COMMIT: again, bringing it
        // straight only; refused again: left open, saying what Revit said.
        private static void Rejoin(
            Document doc, Outcome outcome, EQUIPJOINT joint, Pending pending)
        {
            int onto;

            if (!pending.Map.TryGetValue(joint.MyIndex, out onto))
            {
                Open(outcome, joint, "the new type has no connection for it");

                return;
            }

            string refused = null;

            foreach (bool carry in pending.Carry ? new[] { true, false } : new[] { false })
            {
                var notes = new List<string>();

                using (var transaction = new Transaction(doc, "Change equipment type"))
                {
                    var quiet = new EQUIPFAILURES();

                    transaction.Start();

                    FailureHandlingOptions options =
                        transaction.GetFailureHandlingOptions();

                    options.SetFailuresPreprocessor(quiet);
                    options.SetClearAfterRollback(true);

                    transaction.SetFailureHandlingOptions(options);

                    string why;

                    try
                    {
                        why = joint.Redo(doc, onto, carry, notes);
                    }
                    catch (Exception ex)
                    {
                        why = Short(ex);
                    }

                    if (why != null)
                    {
                        transaction.RollBack();

                        Open(outcome, joint, why + (refused == null
                            ? string.Empty
                            : " (Revit refused carrying it over: " + refused + ")"));

                        return;
                    }

                    TransactionStatus status;

                    try
                    {
                        status = transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        EQUIPLOG.Blew("committing " + joint.Called, ex);

                        status = TransactionStatus.Error;
                    }

                    if (status == TransactionStatus.Committed &&
                        Joined(doc, outcome.Id, onto))
                    {
                        outcome.Notes.AddRange(notes);

                        return;
                    }

                    if (transaction.HasStarted() && !transaction.HasEnded())
                        transaction.RollBack();

                    refused = string.IsNullOrEmpty(quiet.Said)
                        ? "it did not hold"
                        : quiet.Said;

                    EQUIPLOG.Say("   " + joint.Called + ": Revit refused that at the " +
                                 "commit - " + refused +
                                 (carry ? "; AGAIN, bringing it straight only" : string.Empty));
                }
            }

            Open(outcome, joint, "Revit refused joining it (" + refused + ")");
        }

        private static void Open(Outcome outcome, EQUIPJOINT joint, string why)
        {
            outcome.LeftOpen.Add(joint.Called + " left open: " + why);

            EQUIPLOG.Say("   " + joint.Called + " left open: " + why);
        }

        // The unit's connector is joined to something now
        private static bool Joined(Document doc, long unitId, int onto)
        {
            try
            {
                Connector mine = QADUCTS.Lookup(doc, unitId, onto);

                return mine != null && mine.IsConnected;
            }
            catch
            {
                return false;
            }
        }

        // Move the unit so the run on its BIGGEST duct connection that
        // cannot reach as it is, can (EQUIPREACH.Anchor)
        private static void OntoItsDuct(
            Document doc,
            FamilyInstance instance,
            List<EQUIPJOINT> joins,
            Dictionary<int, int> map,
            Outcome outcome)
        {
            EQUIPJOINT anchor = null;

            EQUIPV best = new EQUIPV(0, 0, 0);

            double biggest = -1.0;

            double shortest = EQUIPJOINT.Shortest(doc);

            foreach (EQUIPJOINT joint in joins)
            {
                if (joint.Pipe)
                    continue;

                int onto;

                if (!map.TryGetValue(joint.MyIndex, out onto))
                    continue;

                Connector mine = QADUCTS.Lookup(doc, outcome.Id, onto);

                if (mine == null || joint.TurnedOn(mine))
                    continue;

                XYZ here, far;

                if (!joint.Run(doc, out here, out far))
                    continue;

                EQUIPV move = EQUIPREACH.Anchor(
                    EQUIPWALK.V(here), EQUIPWALK.V(far),
                    EQUIPWALK.V(mine.Origin), shortest);

                if (move.Length < EQUIPREACH.Coincident)
                    continue;

                double size = Area(EQUIPWALK.SizeOf(mine));

                if (size > biggest)
                {
                    biggest = size;
                    best = move;
                    anchor = joint;
                }
            }

            if (anchor == null)
                return;

            var by = new XYZ(best.X, best.Y, best.Z);

            try
            {
                if (instance.Pinned)
                {
                    EQUIPLOG.Say("   the unit is pinned - not moved onto " +
                                 "its duct");

                    return;
                }

                ElementTransformUtils.MoveElement(doc, instance.Id, by);

                doc.Regenerate();
            }
            catch (Exception ex)
            {
                EQUIPLOG.Blew("moving the unit onto its duct", ex);

                return;
            }

            string said = "moved the unit " + EQUIPREACH.Mm(by.GetLength()) +
                          " mm to keep " + anchor.Called + " on its duct";

            outcome.Notes.Add(said);

            EQUIPLOG.Say("   " + said + " (" + EQUIPREACH.Mm(by.X) + ", " +
                         EQUIPREACH.Mm(by.Y) + ", " + EQUIPREACH.Mm(by.Z) +
                         " mm)");
        }

        private static double Area(EQUIPSIZE size)
        {
            if (size == null || !size.Known)
                return 0.0;

            return size.Round
                ? Math.PI * size.Diameter * size.Diameter / 4.0
                : size.Width * size.Height;
        }

        private static bool SameFamily(FamilyInstance instance, FamilySymbol symbol)
        {
            try
            {
                return instance.Symbol != null &&
                       instance.Symbol.Family != null &&
                       symbol.Family != null &&
                       instance.Symbol.Family.Id.ToValue() ==
                       symbol.Family.Id.ToValue();
            }
            catch
            {
                return false;
            }
        }

        // For the log: a unit in a model group is changed in every copy
        // of the group, and its ducts may not be
        private static void Grouped(Document doc, FamilyInstance instance)
        {
            try
            {
                ElementId group = instance.GroupId;

                if (group == null || group == ElementId.InvalidElementId)
                    return;

                Element owner = doc.GetElement(group);

                EQUIPLOG.Say("   in model group " +
                             (owner == null ? group.ToValue().ToString() : owner.Name));
            }
            catch
            {
            }
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
