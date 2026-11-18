using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Drives a branch of ductwork to a size.
    //
    // "SET THE DUCTS AND THE FITTINGS FOLLOW" IS FALSE THROUGH THE API,
    // exactly as it is for pipes - CLAUDE.md records that being learned
    // the hard way on the CHW page, from a real model where the pipes
    // resized and the fittings stayed as they were. So the fittings are
    // driven here too, gentlest route first, and the same two routes
    // work for the same reasons.
    public static class EQUIPSIZER
    {
        // How many elements actually took the new size
        public static int Drive(
            Document doc, List<long> ids, EQUIPSIZE wanted)
        {
            int done = 0;

            if (doc == null || ids == null || !wanted.Known)
                return done;

            // The ducts first. A fitting takes its size from the ducts
            // on it, so doing them the other way round asks a fitting
            // to match something that has not changed yet.
            foreach (long id in ids)
            {
                Element element = doc.GetElement(REVITCOMPAT.NewId(id));

                if (element is MEPCurve && Duct(doc, element, wanted))
                    done++;
            }

            doc.Regenerate();

            foreach (long id in ids)
            {
                Element element = doc.GetElement(REVITCOMPAT.NewId(id));

                if (element == null || element is MEPCurve)
                    continue;

                if (Fitting(doc, element, wanted))
                    done++;
            }

            return done;
        }

        // ==================================================
        // A duct
        // ==================================================

        private static bool Duct(
            Document doc, Element duct, EQUIPSIZE wanted)
        {
            EQUIPSIZE now = EQUIPWALK.SizeOf(duct);

            if (now.Same(wanted))
                return false;

            // A round duct cannot be given a width, and a rectangular
            // one cannot be given a diameter. Changing the SHAPE of a
            // duct is not a resize - it needs a different duct type and
            // different fittings - so it is refused rather than forced.
            if (now.Known && now.Round != wanted.Round)
            {
                EQUIPLOG.Say("   " + Named(duct) + ": " + now +
                             " is a different shape from " + wanted +
                             ", left alone");

                return false;
            }

            try
            {
                if (wanted.Round)
                {
                    if (!Set(duct, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM,
                            wanted.Diameter))
                        return false;
                }
                else
                {
                    bool width = Set(
                        duct, BuiltInParameter.RBS_CURVE_WIDTH_PARAM,
                        wanted.Width);

                    bool height = Set(
                        duct, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM,
                        wanted.Height);

                    if (!width && !height)
                        return false;
                }

                EQUIPLOG.Say("   " + Named(duct) + ": " + now + " -> " +
                             wanted);

                return true;
            }
            catch (Exception ex)
            {
                EQUIPLOG.Blew("resizing " + Named(duct), ex);

                return false;
            }
        }

        private static bool Set(
            Element element, BuiltInParameter which, double feet)
        {
            Parameter parameter = element.get_Parameter(which);

            if (parameter == null || parameter.IsReadOnly)
                return false;

            return parameter.Set(feet);
        }

        // ==================================================
        // A fitting or an accessory
        // ==================================================

        private static bool Fitting(
            Document doc, Element fitting, EQUIPSIZE wanted)
        {
            // Route one: tell each connector its new size. Autodesk's
            // own words about Connector.Radius are that "in order to set
            // this property, it must be mapped to a writable instance
            // parameter in the family definition" - which a lookup-table
            // family is not. So it is set, regenerated and READ BACK:
            // a family that quietly reverts is only caught by looking
            // again. QACHWFITTINGS learned this on the pipe side.
            bool asked = false;

            foreach (Connector connector in EQUIPWALK.EndsOf(fitting))
            {
                EQUIPSIZE now = EQUIPWALK.SizeOf(connector);

                if (now.Same(wanted))
                    continue;

                if (now.Known && now.Round != wanted.Round)
                    continue;

                try
                {
                    if (wanted.Round)
                        connector.Radius = wanted.Diameter / 2.0;
                    else
                    {
                        connector.Width = wanted.Width;
                        connector.Height = wanted.Height;
                    }

                    asked = true;
                }
                catch
                {
                    // Not writable on this family: the read-back below
                    // reports it the same way
                }
            }

            if (!asked)
                return false;

            doc.Regenerate();

            foreach (Connector connector in EQUIPWALK.EndsOf(fitting))
            {
                EQUIPSIZE now = EQUIPWALK.SizeOf(connector);

                if (now.Known && !now.Same(wanted))
                {
                    // It took the write and went straight back to what
                    // its lookup table says. Reported, not fought with:
                    // a fitting whose size is wrong is visible on the
                    // drawing and can be swapped by hand, and replacing
                    // it here would throw away whatever is written on it.
                    EQUIPLOG.Say("   " + Named(fitting) +
                                 ": would not take " + wanted +
                                 ", it is still " + now);

                    return false;
                }
            }

            EQUIPLOG.Say("   " + Named(fitting) + ": now " + wanted);

            return true;
        }

        // WHERE THE RESIZE STOPPED AT A DUCT IT DID NOT RESIZE - one with
        // a tap-off on its side, walking back from a grille - the last
        // resized piece and that duct now differ in size: a transition
        // goes in between them, from the routing preferences. Null when
        // none was needed; else what happened, for the report.
        public static string Bridge(Document doc, long lastId, long stopId)
        {
            Element last = doc.GetElement(REVITCOMPAT.NewId(lastId));

            ConnectorManager manager = QADUCTS.Manager(last);

            if (manager == null)
                return null;

            Connector mine = null;
            Connector theirs = null;

            foreach (Connector c in manager.Connectors)
            {
                foreach (Connector other in c.AllRefs)
                {
                    if (other.Owner != null && other.Owner.Id.ToValue() == stopId)
                    {
                        mine = c;
                        theirs = other;
                    }
                }
            }

            if (mine == null || theirs == null)
                return null;

            EQUIPSIZE a = EQUIPWALK.SizeOf(mine);
            EQUIPSIZE b = EQUIPWALK.SizeOf(theirs);

            if (!a.Known || !b.Known || a.Same(b))
                return null;

            int mineId = mine.Id;
            int theirsId = theirs.Id;

            using (var step = new SubTransaction(doc))
            {
                step.Start();

                try
                {
                    mine.DisconnectFrom(theirs);

                    doc.Regenerate();

                    Connector resized = QADUCTS.Lookup(doc, lastId, mineId);
                    Connector kept = QADUCTS.Lookup(doc, stopId, theirsId);

                    // "The owner of connector1 should be a (flex) duct or
                    // pipe" - the API's rule; the resized side is often a
                    // damper (grille 227's dry run, 2026-11-18)
                    FamilyInstance fitting = kept.Owner is MEPCurve
                        ? doc.Create.NewTransitionFitting(kept, resized)
                        : doc.Create.NewTransitionFitting(resized, kept);

                    if (fitting == null)
                    {
                        step.RollBack();

                        return "sizes differ (" + a + " against " + b +
                               ") and no transition would go in";
                    }

                    doc.Regenerate();

                    step.Commit();

                    EQUIPLOG.Say("   transition " + fitting.Id.ToValue() +
                                 " put in: " + a + " to " + b);

                    return "transition put in where it meets the duct it stopped at (" +
                           a + " to " + b + ")";
                }
                catch (Exception ex)
                {
                    step.RollBack();

                    EQUIPLOG.Blew("putting a transition in", ex);

                    return "sizes differ (" + a + " against " + b +
                           ") - a transition would not go in: " + EQUIPCHANGE.Short(ex);
                }
            }
        }

        private static string Named(Element element)
        {
            return EQUIPWALK.Describe(element) + " " +
                   element.Id.ToValue();
        }
    }
}
