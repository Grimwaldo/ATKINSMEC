using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    // EQUIPMENT CHANGER. Swaps mechanical equipment or air terminals
    // for another type and brings the ductwork and pipework on them
    // with it.
    //
    // PICK, FINISH, A TYPE, OK (user, 2026-11-18: "instead of using a big
    // window, we can make a revit element selection where you select the
    // element to be changed then after pressing finish it will give a
    // list of the types of the family ... and the resize the ductwork
    // checkbox to be removed"). It was a modeless window until then; now
    // it is one modal command, so it runs in the command's own API
    // context and needs no external event. The ductwork is always
    // resized.
    [Transaction(TransactionMode.Manual)]
    public class EQUIPCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIApplication app = commandData.Application;
            UIDocument uiDoc = app.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "Open a model first.";

                return Result.Failed;
            }

            Document doc = uiDoc.Document;

            // ---- what to change: picked in the model, Finish ----------
            List<long> picked;

            try
            {
                var already = new List<Reference>();

                foreach (long id in Wanted(doc, uiDoc.Selection.GetElementIds()))
                    already.Add(new Reference(doc.GetElement(REVITCOMPAT.NewId(id))));

                IList<Reference> refs = uiDoc.Selection.PickObjects(
                    ObjectType.Element,
                    new EQUIPFILTER(),
                    "Pick the equipment or air terminals to change, then press Finish",
                    already);

                picked = Wanted(doc, refs.Select(r => r.ElementId).ToList());
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            if (picked.Count == 0)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Equipment Changer",
                    "Nothing to change was picked - pick mechanical equipment or air terminals.");

                return Result.Cancelled;
            }

            // ---- which type: only the picked units' own family's -----
            var current = new HashSet<long>();
            var families = new HashSet<long>();

            foreach (long id in picked)
            {
                var unit = doc.GetElement(REVITCOMPAT.NewId(id)) as FamilyInstance;

                if (unit == null || unit.Symbol == null)
                    continue;

                current.Add(unit.Symbol.Id.ToValue());

                if (unit.Symbol.Family != null)
                    families.Add(unit.Symbol.Family.Id.ToValue());
            }

            var rows = new List<EQUIPPICK.Row>();

            foreach (FamilySymbol symbol in EQUIPTYPES.For(doc, picked, false))
            {
                rows.Add(new EQUIPPICK.Row
                {
                    Id = symbol.Id.ToValue(),
                    Label = families.Count > 1 ? EQUIPTYPES.Label(symbol) : symbol.Name,
                    Current = current.Contains(symbol.Id.ToValue())
                });
            }

            string said = picked.Count == 1
                ? "1 picked. Change it to:"
                : picked.Count + " picked" +
                  (families.Count > 1 ? ", " + families.Count + " families" : string.Empty) +
                  ". Change them to:";

            long chosen;

            using (var window = new EQUIPPICK(said, rows))
            {
                if (window.ShowDialog(new EQUIPOWNER(app.MainWindowHandle)) !=
                    System.Windows.Forms.DialogResult.OK)
                    return Result.Cancelled;

                chosen = window.Chosen;
            }

            var type = doc.GetElement(REVITCOMPAT.NewId(chosen)) as FamilySymbol;

            if (type == null)
                return Result.Cancelled;

            // Only the units of the chosen type's family
            var units = picked.Where(id =>
            {
                var unit = doc.GetElement(REVITCOMPAT.NewId(id)) as FamilyInstance;

                return unit != null && unit.Symbol != null && unit.Symbol.Family != null &&
                       type.Family != null &&
                       unit.Symbol.Family.Id.ToValue() == type.Family.Id.ToValue();
            }).ToList();

            // ---- doing it -------------------------------------------
            EQUIPCHANGE.Result result = EQUIPCHANGE.Run(doc, units, chosen, true);

            Report(result, picked.Count - units.Count);

            try
            {
                var changed = result.Outcomes
                    .Where(o => o.Changed)
                    .Select(o => REVITCOMPAT.NewId(o.Id))
                    .ToList();

                if (changed.Count > 0)
                    uiDoc.Selection.SetElementIds(changed);
            }
            catch
            {
            }

            return Result.Succeeded;
        }

        // What happened, short; what was left open in the details
        private static void Report(EQUIPCHANGE.Result result, int otherFamily)
        {
            var dialog = new Autodesk.Revit.UI.TaskDialog("Equipment Changer");

            if (!string.IsNullOrEmpty(result.Blocked))
            {
                dialog.MainInstruction = result.Blocked;
                dialog.Show();

                return;
            }

            dialog.MainInstruction = result.Changed == 0
                ? "Nothing was changed"
                : result.Changed + " changed" +
                  (result.Open > 0 ? " - " + result.Open + " with a connection left open" : string.Empty);

            var said = new List<string>();

            if (result.Refused > 0)
                said.Add(result.Refused + " refused - put back as it was.");

            if (result.Already > 0)
                said.Add(result.Already + " already that type.");

            if (otherFamily > 0)
                said.Add(otherFamily + " of another family left alone.");

            dialog.MainContent = string.Join(Environment.NewLine, said);

            var detail = new StringBuilder();

            foreach (EQUIPCHANGE.Outcome o in result.Outcomes)
            {
                if (o.AlreadyThat)
                    continue;

                detail.AppendLine(o.Name + ": " + o.WasType + " -> " + o.NowType +
                                  (o.Changed ? string.Empty : " - REFUSED: " + o.Said));

                foreach (string line in o.LeftOpen)
                    detail.AppendLine("   OPEN: " + line);

                foreach (string line in o.Notes)
                    detail.AppendLine("   " + line);
            }

            if (detail.Length > 0)
                dialog.ExpandedContent = detail.ToString().TrimEnd();

            dialog.FooterText = "Every step is in " + EQUIPLOG.Where;

            dialog.Show();
        }

        // Only the two kinds this can change. Anything else in the
        // selection is left alone rather than complained about - people
        // rubber-band a region and expect the tool to know its own
        // business.
        public static List<long> Wanted(
            Document doc, ICollection<ElementId> ids)
        {
            var wanted = new List<long>();

            if (doc == null || ids == null)
                return wanted;

            foreach (ElementId id in ids)
            {
                Element element = doc.GetElement(id);

                if (EQUIPFILTER.Changeable(element))
                    wanted.Add(element.Id.ToValue());
            }

            return wanted;
        }
    }

    // What the pick lets through: mechanical equipment and air terminals
    public class EQUIPFILTER : ISelectionFilter
    {
        public static bool Changeable(Element element)
        {
            if (!(element is FamilyInstance) || element.Category == null)
                return false;

            long category = element.Category.Id.ToValue();

            return category == (long)BuiltInCategory.OST_MechanicalEquipment ||
                   category == (long)BuiltInCategory.OST_DuctTerminal;
        }

        public bool AllowElement(Element elem)
        {
            return Changeable(elem);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    // The button is only worth pressing with a model open, but it is
    // left available so the command can explain itself rather than the
    // button being mysteriously grey
    public class EQUIPAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(
            UIApplication applicationData,
            CategorySet selectedCategories)
        {
            return true;
        }
    }
}
