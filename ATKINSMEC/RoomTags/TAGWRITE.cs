using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace ATKINSMEC
{
    // ROOM TAGS' WRITE: one room tag per missing room, in the view its
    // annotation belongs to (TAGPLAN.ToTag), at the room's own point - one
    // transaction, undone as one. Runs in the command's context (the
    // window is modal).
    public static class TAGWRITE
    {
        public static TAGPLAN.Done Tag(Document doc, Dictionary<long, List<TAGPLAN.Room>> plan, long typeId)
        {
            var done = new TAGPLAN.Done();
            var made = new List<ElementId>();

            ElementId type = typeId == 0 ? null : REVITCOMPAT.NewId(typeId);

            using (var transaction = new Transaction(doc, "Room tags"))
            {
                FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new Warnings());
                transaction.SetFailureHandlingOptions(options);

                transaction.Start();

                foreach (KeyValuePair<long, List<TAGPLAN.Room>> owner in plan ?? new Dictionary<long, List<TAGPLAN.Room>>())
                {
                    var view = doc.GetElement(REVITCOMPAT.NewId(owner.Key)) as View;

                    if (view == null)
                    {
                        done.Asked += owner.Value.Count;
                        done.Failed.Add("view " + owner.Key + " is gone (" + owner.Value.Count + " room(s))");
                        continue;
                    }

                    done.Views++;

                    foreach (TAGPLAN.Room room in owner.Value)
                    {
                        done.Asked++;

                        try
                        {
                            RoomTag tag = doc.Create.NewRoomTag(
                                new LinkElementId(REVITCOMPAT.NewId(room.Link), REVITCOMPAT.NewId(room.Id)),
                                new UV(room.X, room.Y),
                                view.Id);

                            if (tag == null)
                            {
                                done.Failed.Add(Said(view, room, "Revit made no tag"));
                                continue;
                            }

                            if (type != null && tag.GetTypeId() != type)
                                tag.ChangeTypeId(type);

                            made.Add(tag.Id);
                        }
                        catch (Exception ex)
                        {
                            done.Failed.Add(Said(view, room, ex.Message));
                        }
                    }
                }

                TransactionStatus status = transaction.Commit();

                if (status != TransactionStatus.Committed)
                    done.Error = "The tags were not kept (" + status + ").";
            }

            // REPORT FROM THE MODEL: the tags that are there after the commit
            done.Placed = made.Count(id => doc.GetElement(id) is RoomTag);

            return done;
        }

        // THE DUPLICATES TAGPLAN.Doubles chose, deleted - one transaction,
        // undone as one; counted from the model after the commit
        public static TAGPLAN.Removal Remove(Document doc, IList<long> ids)
        {
            var removal = new TAGPLAN.Removal();
            var asked = new List<ElementId>();

            using (var transaction = new Transaction(doc, "Remove duplicate room tags"))
            {
                FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new Warnings());
                transaction.SetFailureHandlingOptions(options);

                transaction.Start();

                foreach (long value in ids ?? new List<long>())
                {
                    ElementId id = REVITCOMPAT.NewId(value);

                    removal.Asked++;

                    try
                    {
                        // Only ever a room tag, whatever the id
                        if (!(doc.GetElement(id) is RoomTag))
                        {
                            removal.Failed.Add("tag " + value + " - not a room tag any more");
                            continue;
                        }

                        doc.Delete(id);
                        asked.Add(id);
                    }
                    catch (Exception ex)
                    {
                        removal.Failed.Add("tag " + value + " - " + ex.Message);
                    }
                }

                TransactionStatus status = transaction.Commit();

                if (status != TransactionStatus.Committed)
                    removal.Error = "The tags were not removed (" + status + ").";
            }

            // REPORT FROM THE MODEL: the tags that are gone after the commit
            removal.Removed = asked.Count(id => doc.GetElement(id) == null);

            return removal;
        }

        private static string Said(View view, TAGPLAN.Room room, string why)
        {
            return view.Name + ": " + room.Number + " " + room.Name + " - " + why;
        }

        // A tag placed where it overlaps something is Revit telling you
        // what it has already accepted: warnings go, errors stay Revit's
        private class Warnings : IFailuresPreprocessor
        {
            public FailureProcessingResult PreprocessFailures(FailuresAccessor failures)
            {
                try
                {
                    foreach (FailureMessageAccessor message in failures.GetFailureMessages())
                    {
                        if (message.GetSeverity() == FailureSeverity.Warning)
                            failures.DeleteWarning(message);
                    }
                }
                catch
                {
                }

                return FailureProcessingResult.Continue;
            }
        }
    }
}
