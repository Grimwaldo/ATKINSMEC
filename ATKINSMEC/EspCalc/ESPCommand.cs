using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ATKINSMEC
{
    // ESP - external static pressure for ductwork, across this model and
    // any linked models whose ductwork joins it.
    //
    // The window is MODAL, like CHW VOLUME, so every callback below runs
    // inside this command's API context: Calculate reads the models
    // directly and Write opens its own transaction. Manual, because Write
    // changes the model.
    [Transaction(TransactionMode.Manual)]
    public class ESPCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;

            if (uidoc == null)
            {
                TaskDialog.Show("ESP", "Open a model first.");
                return Result.Cancelled;
            }

            Document doc = uidoc.Document;

            // Units selected before the button was pressed start ticked
            var picked = new HashSet<long>();

            foreach (ElementId id in uidoc.Selection.GetElementIds())
                picked.Add(id.ToValue());

            List<ESPLINKROW> links = RVTLINKSELECTION.GetLinks(doc)
                .Select(l => new ESPLINKROW { Id = l.Id, Label = l.Name })
                .ToList();

            string title = doc.Title;

            Func<List<long>, List<ESPUNITROW>> readUnits =
                linkIds => ESPCOLLECT.Units(doc, linkIds, picked);

            Func<List<long>, List<string>, ESPSETTINGS, ESPRUN> calculate =
                (linkIds, keys, settings) => Calculate(doc, linkIds, keys, settings, title);

            Func<List<KeyValuePair<string, double>>, string, string> write =
                (values, parameter) => ESPCOLLECT.Write(doc, values, parameter);

            List<ESPUNITROW> start = readUnits(new List<long>());

            using (var form = new ESPForm(
                       title,
                       links,
                       start.Where(u => u.Picked).Select(u => u.Key).ToList(),
                       readUnits,
                       calculate,
                       write,
                       ESPSETTINGS.Load()))
            {
                form.SetUnits(start);
                form.ShowDialog();
            }

            return Result.Succeeded;
        }

        private static ESPRUN Calculate(
            Document doc, List<long> linkIds, List<string> keys, ESPSETTINGS settings, string title)
        {
            var run = new ESPRUN { TableBuiltIn = settings.TableIsBuiltIn() };

            List<ESPNET.Part> parts = ESPCOLLECT.Read(doc, linkIds, run.Models);

            run.Parts = parts.Count;

            List<ESPNET.Bridge> misses;

            run.Bridges = ESPNET.BridgeModels(parts, settings.ToleranceMm / 1000.0, out misses);
            run.NearMisses = misses;

            var byKey = new Dictionary<string, ESPNET.Part>();

            foreach (ESPNET.Part part in parts)
            {
                if (!byKey.ContainsKey(part.Key))
                    byKey.Add(part.Key, part);
            }

            foreach (string key in keys)
            {
                ESPNET.Part unit;

                if (!byKey.TryGetValue(key, out unit) || unit.What != ESPNET.Kind.Equipment)
                {
                    run.Missing.Add(key);
                    continue;
                }

                run.Results.Add(ESPNET.Calculate(unit, settings));

                run.InModel[key] = ESPCOLLECT.InModel(doc, unit.LinkId, unit.ElementId, settings.Parameter);
            }

            run.LogPath = ESPLOG.Write(run, settings, title);

            return run;
        }
    }

    // Live with a model open and nothing selected: the window has its own
    // list of units, so a selection is a convenience, not a requirement
    public class ESPAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return applicationData.ActiveUIDocument != null;
        }
    }
}
