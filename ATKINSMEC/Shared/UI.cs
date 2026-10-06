using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using System.Reflection;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace ATKINSMEC
{
 
        public class UI : IExternalApplication
        {
            // ==================================================
            // The ribbon icons
            // ==================================================

            // Drawn by tools/make-icons.py and embedded; see the csproj
            // for why the resource name is pinned.
            //
            // CacheOption.OnLoad IS NOT OPTIONAL. A BitmapImage reads
            // its StreamSource LAZILY by default, so the stream would be
            // disposed by the using block before anything drew the
            // image - which shows up as a blank button rather than as an
            // error. OnLoad reads it now, while the stream is alive.
            //
            // Freeze() afterwards because Revit puts these on its own UI
            // thread, and an unfrozen one belongs to whichever thread
            // made it.
            private static ImageSource Icon(string name, int size)
            {
                try
                {
                    string resource =
                        "ATKINSMEC.Icons." + name + size + ".png";

                    using (Stream stream = Assembly
                        .GetExecutingAssembly()
                        .GetManifestResourceStream(resource))
                    {
                        if (stream == null)
                            return null;

                        var image = new BitmapImage();

                        image.BeginInit();
                        image.StreamSource = stream;
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.EndInit();
                        image.Freeze();

                        return image;
                    }
                }
                catch
                {
                    // A MISSING ICON MUST NOT COST THE BUTTON. The
                    // ribbon is built in OnStartup, and throwing here
                    // would take the whole add-in down over a picture.
                    return null;
                }
            }

            private static void Dress(PushButtonData data, string icon)
            {
                ImageSource large = Icon(icon, 32);
                ImageSource small = Icon(icon, 16);

                if (large != null)
                    data.LargeImage = large;

                if (small != null)
                    data.Image = small;
            }

            private UIControlledApplication application;
            public Result OnShutdown(UIControlledApplication application)
            {
                DUCTINSULATIONUPDATER.Unregister();

                // A timer left running while Revit tears itself down
                // would raise an external event into a dead application
                SYNCSERVICE.Shutdown();

                return Result.Succeeded;
            }

            public Result OnStartup(UIControlledApplication application)
            {
                this.application = application;
             createpushbutton();

            // Watches for new ducts and insulates them; does nothing
            // until the rules are switched on in the DUCT INSULATION window
            DUCTINSULATIONUPDATER.Register(application.ActiveAddInId);

            // AutoSync asks whether to run, but NOT here: OnStartup is
            // too early to put a dialog in front of somebody, and
            // ExternalEvent.Create wants a live application. Both wait
            // for Revit to say it has finished starting.
            application.ControlledApplication.ApplicationInitialized
                += RevitIsUp;

            return Result.Succeeded;
        }

            // Fires once, after Revit is up and before any model is
            // open. Unhooks itself: the question is asked per session.
            private void RevitIsUp(
                object sender, ApplicationInitializedEventArgs e)
            {
                try
                {
                    if (this.application != null)
                    {
                        this.application.ControlledApplication
                            .ApplicationInitialized -= RevitIsUp;
                    }
                }
                catch
                {
                    // unhooking is tidiness, not correctness
                }

                try
                {
                    SYNCSETTINGS.Load();

                    // Must happen on Revit's own thread, which this is
                    SYNCSERVICE.Prepare();

                    using (SYNCASKForm ask = new SYNCASKForm())
                    {
                        ask.ShowDialog();

                        if (ask.TurnOn)
                            SYNCSERVICE.Start(ask.Minutes);
                    }
                }
                catch (System.Exception ex)
                {
                    // Nothing about AutoSync is worth stopping Revit
                    // from starting for
                    SYNCLOG.Add(SYNCLOG.What.Failed, string.Empty,
                        "could not start: " + ex.Message);
                }
            }

            public void createpushbutton()
            {
                string tabName = "ATKINS";
                string panelname = "Mechanical";
                string path = Assembly.GetExecutingAssembly().Location;
                try
                {
                    application.CreateRibbonTab(tabName);
                }
                catch (Exception) { }



                List<RibbonPanel> panels = application.GetRibbonPanels(tabName);
                RibbonPanel panel = panels.FirstOrDefault(p => p.Name == panelname);
                if (panel == null)
                {
                    panel = application.CreateRibbonPanel(tabName, panelname);
                }

                PushButtonData pushbuttondata = new PushButtonData("Tool1", "AUTOVENT ", path, "ATKINSMEC.Command");
                Dress(pushbuttondata, "autovent");
                PushButton pushbutton = panel.AddItem(pushbuttondata) as PushButton;

                PushButtonData chwbuttondata = new PushButtonData("Tool2", "CHW VOLUME", path, "ATKINSMEC.CHWCommand");
                Dress(chwbuttondata, "chwvolume");
                chwbuttondata.ToolTip = "Total chilled water volume held by the pipes and fittings of two chosen piping system types, in this model and its links.";
                // Alone among the four, this one is useful with no model
                // open: an ACC model is measured in the background
                chwbuttondata.AvailabilityClassName = "ATKINSMEC.CHWAvailability";
                PushButton chwbutton = panel.AddItem(chwbuttondata) as PushButton;

                PushButtonData insbuttondata = new PushButtonData("Tool3", "DUCT INSULATION", path, "ATKINSMEC.INSULATIONCommand");
                Dress(insbuttondata, "ductinsulation");
                insbuttondata.ToolTip = "Insulation thickness per duct system type, applied automatically to ducts as they are drawn.";
                PushButton insbutton = panel.AddItem(insbuttondata) as PushButton;

                PushButtonData sprbuttondata = new PushButtonData("Tool4", "SPRINKLERS", path, "ATKINSMEC.SPRINKLERCommand");
                Dress(sprbuttondata, "sprinklers");
                sprbuttondata.ToolTip = "Distributes pendent concealed sprinklers room by room to NFPA 13 spacing, from the rooms of a linked architectural model.";
                PushButton sprbutton = panel.AddItem(sprbuttondata) as PushButton;

                PushButtonData alignbuttondata = new PushButtonData("Tool5", "ALIGN TERMINALS", path, "ATKINSMEC.ALIGNCommand");
                Dress(alignbuttondata, "alignterminals");
                alignbuttondata.ToolTip = "Moves this model's air terminals onto the nearest air terminal in a linked architectural model, in plan and at the same height.";
                PushButton alignbutton = panel.AddItem(alignbuttondata) as PushButton;

                PushButtonData qabuttondata = new PushButtonData("Tool6", "SHEET QA", path, "ATKINSMEC.QACommand");
                Dress(qabuttondata, "sheetqa");
                qabuttondata.ToolTip = "Checks what the sheets say before they go out. First check: air terminal, duct and pipe tags reading zero flow. Reads only, never changes the model.";
                PushButton qabutton = panel.AddItem(qabuttondata) as PushButton;

                PushButtonData syncbuttondata = new PushButtonData("Tool7", "AUTOSYNC", path, "ATKINSMEC.SYNCCommand");
                Dress(syncbuttondata, "autosync");
                syncbuttondata.ToolTip = "Saves and synchronises every open model on a timer. Asks each time Revit starts; saves locally instead when central cannot be reached.";
                // About Revit rather than about one model, so it is live
                // with nothing open - same as CHW VOLUME
                syncbuttondata.AvailabilityClassName = "ATKINSMEC.SYNCAvailability";
                PushButton syncbutton = panel.AddItem(syncbuttondata) as PushButton;

                PushButtonData nwcbuttondata = new PushButtonData("Tool8", "NWC BATCH", path, "ATKINSMEC.NWCCommand");
                Dress(nwcbuttondata, "nwcbatch");
                nwcbuttondata.ToolTip = "Exports a Navisworks .nwc from a list of models - files on disk or models in ACC - opening each one in the background and closing it again. Needs the Navisworks exporter installed.";
                // A batch of other models needs no model open, so the
                // button stays alive with nothing loaded - same as CHW
                // VOLUME, and the usual way to run it
                nwcbuttondata.AvailabilityClassName = "ATKINSMEC.NWCAvailability";
                PushButton nwcbutton = panel.AddItem(nwcbuttondata) as PushButton;

                PushButtonData equipbuttondata = new PushButtonData("Tool9", "EQUIPMENT CHANGER", path, "ATKINSMEC.EQUIPCommand");
                Dress(equipbuttondata, "equipchanger");
                equipbuttondata.ToolTip = "Changes the type of the selected mechanical equipment or air terminals and resizes the ductwork on them - the ducts, fittings and accessories - as far as the first reducer, tap-off, tee or air terminal.";
                // Available with a model open but nothing selected, so
                // the command itself can say what to select rather than
                // the button being mysteriously grey
                equipbuttondata.AvailabilityClassName = "ATKINSMEC.EQUIPAvailability";
                PushButton equipbutton = panel.AddItem(equipbuttondata) as PushButton;

                PushButtonData espbuttondata = new PushButtonData("Tool10", "DUCT ESP", path, "ATKINSMEC.ESPCommand");
                Dress(espbuttondata, "ductesp");
                espbuttondata.ToolTip = "External static pressure of the selected or listed units: the index run of each duct connector, across this model and any linked models whose ductwork joins it. Exports to Excel and can write the ESP to the units.";
                // A selection is a convenience, not a requirement - the
                // window lists every unit - so live whenever a model is
                espbuttondata.AvailabilityClassName = "ATKINSMEC.ESPAvailability";
                PushButton espbutton = panel.AddItem(espbuttondata) as PushButton;

            }
        }
    }

