using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace ATKINSMEC
{
    // EQUIPMENT CHANGER. Swaps mechanical equipment or air terminals
    // for another type and brings the ductwork on them with it.
    //
    // MODELESS, at the user's request, and it is the right shape for
    // this: the whole job is "look at that unit, pick a type, look at
    // the next one", which a modal window makes into a loop of opening
    // and closing. The model stays usable, the selection can be changed
    // with the window open, and a row can be shown in the view.
    //
    // The price is that NOTHING here may touch the API directly -
    // everything goes through EQUIPEVENT.
    [Transaction(TransactionMode.Manual)]
    public class EQUIPCommand : IExternalCommand
    {
        // Held so the form and its event survive the command returning
        public static EQUIPForm Form { get; set; }
        public static EQUIPEVENT Handler { get; set; }
        public static ExternalEvent Event { get; set; }

        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "Open a model first.";

                return Result.Failed;
            }

            // A second click brings the open window forward
            if (Form != null && !Form.IsDisposed)
            {
                Form.Activate();

                return Result.Succeeded;
            }

            Handler = new EQUIPEVENT();
            Event = ExternalEvent.Create(Handler);

            Form = new EQUIPForm(uiDoc.Document.Title, Handler, Event);
            Handler.Form = Form;

            Form.Show();

            return Result.Succeeded;
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

                if (element == null || element.Category == null)
                    continue;

                if (!(element is FamilyInstance))
                    continue;

                long category = element.Category.Id.ToValue();

                if (category == (long)BuiltInCategory.OST_MechanicalEquipment ||
                    category == (long)BuiltInCategory.OST_DuctTerminal)
                    wanted.Add(element.Id.ToValue());
            }

            return wanted;
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
