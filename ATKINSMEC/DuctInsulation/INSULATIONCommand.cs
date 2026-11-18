using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // INSULATOR: the rules window for the automatic duct and pipe
    // insulation. The insulating itself is done by INSULATIONUPDATER while
    // ducts and pipes are drawn. The window is Revit-free: it is handed
    // this model's system types and insulation types, read here.
    [Transaction(TransactionMode.ReadOnly)]
    public class INSULATIONCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            var ducts = new INSULATIONForm.Model
            {
                SystemTypes = Names(doc, typeof(MechanicalSystemType)),
                InsulationTypes = Names(doc, typeof(DuctInsulationType))
            };

            var pipes = new INSULATIONForm.Model
            {
                SystemTypes = Names(doc, typeof(PipingSystemType)),
                InsulationTypes = Names(doc, typeof(PipeInsulationType))
            };

            using (INSULATIONForm form = new INSULATIONForm(ducts, pipes))
            {
                form.ShowDialog();
            }

            return Result.Succeeded;
        }

        private static List<string> Names(Document doc, System.Type type)
        {
            return new FilteredElementCollector(doc)
                .OfClass(type)
                .Select(e => e.Name)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .OrderBy(name => name)
                .ToList();
        }
    }
}
