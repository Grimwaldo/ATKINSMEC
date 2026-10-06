using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // Catches what Revit would otherwise put on screen.
    //
    // The error this add-in exists to avoid -
    //
    //     "The family is connected to a network and can no longer keep
    //      the connectivity. Disconnect the family from the network?"
    //
    // - arrives as an error Revit itself labels CANNOT BE IGNORED, four
    // at a time in the model that prompted this. Taking the joints
    // apart first is what stops it happening at all; this is the
    // backstop for the ones nobody predicted.
    //
    // It does NOT resolve errors. Revit's own resolution for that one is
    // "Disconnect the family from the network", which would leave the
    // ductwork silently unhooked - the exact damage this is supposed to
    // prevent. So an error means the element goes back the way it was
    // and is reported, which EQUIPCHANGE reads off Errored.
    public class EQUIPFAILURES : IFailuresPreprocessor
    {
        private readonly List<string> _said = new List<string>();

        public bool Errored { get; private set; }

        public string Said
        {
            get { return string.Join("; ", _said.ToArray()); }
        }

        public void Clear()
        {
            Errored = false;

            _said.Clear();
        }

        public FailureProcessingResult PreprocessFailures(
            FailuresAccessor failures)
        {
            try
            {
                IList<FailureMessageAccessor> messages =
                    failures.GetFailureMessages();

                if (messages == null || messages.Count == 0)
                    return FailureProcessingResult.Continue;

                bool cleared = false;

                foreach (FailureMessageAccessor one in messages)
                {
                    FailureSeverity how = one.GetSeverity();

                    string text = Text(one);

                    if (how == FailureSeverity.Warning)
                    {
                        // A warning is Revit telling you something it
                        // has already decided to live with
                        failures.DeleteWarning(one);

                        cleared = true;

                        EQUIPLOG.Say("   warning cleared: " + text);

                        continue;
                    }

                    Errored = true;

                    if (!_said.Contains(text))
                        _said.Add(text);

                    EQUIPLOG.Say("   ERROR: " + text);
                }

                if (Errored)
                {
                    // Back it out. The caller's SubTransaction rolls
                    // back too, so the element keeps the type and the
                    // ductwork it had.
                    return FailureProcessingResult.ProceedWithRollBack;
                }

                return cleared
                    ? FailureProcessingResult.ProceedWithCommit
                    : FailureProcessingResult.Continue;
            }
            catch (Exception ex)
            {
                EQUIPLOG.Blew("reading Revit's failures", ex);

                return FailureProcessingResult.Continue;
            }
        }

        private static string Text(FailureMessageAccessor one)
        {
            try
            {
                string said = one.GetDescriptionText() ?? string.Empty;

                said = said.Replace("\r", " ").Replace("\n", " ").Trim();

                return said.Length > 200
                    ? said.Substring(0, 200) + "..."
                    : said;
            }
            catch
            {
                return "(a failure that would not describe itself)";
            }
        }
    }
}
