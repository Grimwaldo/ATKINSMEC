using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATKINSMEC
{
    // The only place in AutoSync that touches Revit. Everything else -
    // the timer, the settings, the reachability probe - is deliberately
    // API-free, because a timer tick is no more an API context than a
    // button handler is.
    //
    // Revit raises this when it is IDLE, which is what stops an auto
    // sync interrupting somebody halfway through a command.
    public class SYNCEVENT : IExternalEventHandler
    {
        public enum Why
        {
            Timer,
            Manual
        }

        public Why Reason { get; set; }

        public void Execute(UIApplication app)
        {
            SYNCSERVICE.Busy = true;

            try
            {
                Run(app);
            }
            catch (Exception ex)
            {
                SYNCLOG.Add(SYNCLOG.What.Failed, string.Empty,
                    "the run stopped: " + ex.Message);
            }
            finally
            {
                SYNCSERVICE.Busy = false;

                SYNCLOG.Ran();

                if (SYNCForm.Open != null && !SYNCForm.Open.IsDisposed)
                    SYNCForm.Open.Refreshed();
            }
        }

        public string GetName()
        {
            return "Atkins MEP Toolkit - AutoSync";
        }

        // ==================================================

        private void Run(UIApplication app)
        {
            Application application = app.Application;

            if (application == null)
                return;

            var documents = new List<Document>();

            foreach (Document doc in application.Documents)
            {
                if (doc == null)
                    continue;

                // A linked model is somebody else's to save, and a
                // family in the background is not what was asked for
                if (doc.IsLinked || doc.IsFamilyDocument)
                    continue;

                documents.Add(doc);
            }

            if (documents.Count == 0)
            {
                SYNCLOG.Add(SYNCLOG.What.Skipped, string.Empty,
                    "no models open");

                return;
            }

            // One probe per central location, not one per model: five
            // models off the same server should cost one test
            var probed = new Dictionary<string, SYNCNET.Answer>(
                StringComparer.OrdinalIgnoreCase);

            foreach (Document doc in documents)
                One(doc, probed);
        }

        private void One(
            Document doc, Dictionary<string, SYNCNET.Answer> probed)
        {
            string title;

            try
            {
                title = doc.Title;
            }
            catch
            {
                return;
            }

            try
            {
                // Dynamic, not "opened read-only": Autodesk's own words
                // are that the likeliest reason for this to be true is
                // Revit processing failures at that moment. Either way
                // nothing can be changed now, so leave it for next time.
                if (doc.IsReadOnly)
                {
                    SYNCLOG.Add(SYNCLOG.What.Skipped, title,
                        "Revit would not let this model be changed just "
                            + "now - trying again next time");

                    return;
                }

                // There is deliberately NO Document.IsModifiable check
                // here, and adding one breaks everything.
                //
                // IsModifiable means "a transaction is open on this
                // document right now". This handler has no transaction
                // open and must not have one - SynchronizeWithCentral
                // and Save both have to be called OUTSIDE a transaction.
                // So IsModifiable is false for every document on every
                // run, and guarding on it skipped the entire job every
                // time while looking like a real condition.

                if (!doc.IsWorkshared || doc.IsDetached)
                {
                    Keep(doc, title,
                        doc.IsDetached
                            ? "detached from central, so there is nothing "
                              + "to sync to"
                            : "not workshared, so there is nothing to "
                              + "sync to");

                    return;
                }

                SYNCNET.Answer answer = Reachable(doc, probed);

                if (!answer.Reachable)
                {
                    // The whole point of the connection test: a sync
                    // that cannot reach central would hang or fail, so
                    // the work is put somewhere safe instead
                    Keep(doc, title, answer.Why);

                    return;
                }

                Sync(doc, title);
            }
            catch (Exception ex)
            {
                SYNCLOG.Add(SYNCLOG.What.Failed, title, Short(ex));
            }
        }

        // ==================================================
        // Reaching central
        // ==================================================

        private static SYNCNET.Answer Reachable(
            Document doc, Dictionary<string, SYNCNET.Answer> probed)
        {
            bool cloud = false;

            try
            {
                cloud = doc.IsModelInCloud;
            }
            catch
            {
                // older or odd documents; treat as file-based
            }

            if (cloud)
            {
                return Cached(probed, "cloud",
                    delegate
                    {
                        return SYNCNET.Internet(
                            SYNCSETTINGS.Host, SYNCNET.TimeoutMs);
                    });
            }

            string central = Central(doc);

            if (string.IsNullOrEmpty(central))
            {
                return new SYNCNET.Answer
                {
                    Reachable = false,
                    Why = "the central model path could not be read"
                };
            }

            string folder;

            try
            {
                folder = Path.GetDirectoryName(central) ?? central;
            }
            catch
            {
                folder = central;
            }

            return Cached(probed, folder,
                delegate { return SYNCNET.Share(central); });
        }

        private static SYNCNET.Answer Cached(
            Dictionary<string, SYNCNET.Answer> probed,
            string key,
            Func<SYNCNET.Answer> test)
        {
            SYNCNET.Answer already;

            if (probed.TryGetValue(key, out already))
                return already;

            SYNCNET.Answer fresh = test();

            probed[key] = fresh;

            return fresh;
        }

        public static string Central(Document doc)
        {
            try
            {
                ModelPath path = doc.GetWorksharingCentralModelPath();

                if (path == null)
                    return null;

                return ModelPathUtils
                    .ConvertModelPathToUserVisiblePath(path);
            }
            catch
            {
                return null;
            }
        }

        // ==================================================
        // Doing it
        // ==================================================

        private static void Sync(Document doc, string title)
        {
            var transact = new TransactWithCentralOptions();

            var options = new SynchronizeWithCentralOptions();

            options.Comment = string.IsNullOrWhiteSpace(SYNCSETTINGS.Comment)
                ? "Auto sync"
                : SYNCSETTINGS.Comment;

            // Save the local either side. If the sync itself falls over,
            // the work is still on disk rather than only in memory.
            options.SaveLocalBefore = true;
            options.SaveLocalAfter = true;
            options.Compact = false;

            var giveBack = new RelinquishOptions(false);

            giveBack.StandardWorksets = SYNCSETTINGS.Relinquish;
            giveBack.ViewWorksets = SYNCSETTINGS.Relinquish;
            giveBack.FamilyWorksets = SYNCSETTINGS.Relinquish;
            giveBack.UserWorksets = SYNCSETTINGS.Relinquish;
            giveBack.CheckedOutElements = SYNCSETTINGS.Relinquish;

            options.SetRelinquishOptions(giveBack);

            doc.SynchronizeWithCentral(transact, options);

            SYNCLOG.Add(SYNCLOG.What.Synced, title,
                SYNCSETTINGS.Relinquish
                    ? "synced and relinquished"
                    : "synced");
        }

        // The fallback: keep the work on disk when central is out of
        // reach, or when there is no central to reach
        private static void Keep(Document doc, string title, string why)
        {
            bool modified;

            try
            {
                modified = doc.IsModified;
            }
            catch
            {
                modified = true;
            }

            if (!modified)
            {
                SYNCLOG.Add(SYNCLOG.What.Skipped, title,
                    why + " - and nothing has changed to save");

                return;
            }

            if (string.IsNullOrWhiteSpace(doc.PathName))
            {
                // Never saved anywhere: Save() would need a path, and
                // choosing one on somebody's behalf is not this add-in's
                // business
                SYNCLOG.Add(SYNCLOG.What.Skipped, title,
                    why + " - and the model has never been saved, so "
                        + "there is nowhere to put it");

                return;
            }

            try
            {
                doc.Save();

                SYNCLOG.Add(SYNCLOG.What.Saved, title,
                    "saved locally instead: " + why);
            }
            catch (Exception ex)
            {
                SYNCLOG.Add(SYNCLOG.What.Failed, title,
                    "could not save: " + Short(ex));
            }
        }

        private static string Short(Exception ex)
        {
            string message = (ex.Message ?? string.Empty).Trim();

            if (message.Length > 160)
                message = message.Substring(0, 157) + "...";

            return message.Length == 0 ? ex.GetType().Name : message;
        }
    }
}
