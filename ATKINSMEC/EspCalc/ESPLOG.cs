using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ATKINSMEC
{
    // Desktop\ATKINSMEC_esp_log.txt, written on every Calculate.
    //
    // The other add-ins that change or judge a real model all earned a log
    // the hard way: the first run on a real job is where the surprises
    // are, and a log turns a round of guessing into one read. This one
    // says what was read from each model, EVERY joint made between models
    // (and the near misses that were not), and each unit's runs with
    // their index steps and where each figure came from. Revit-free.
    public static class ESPLOG
    {
        private const string FileName = "ATKINSMEC_esp_log.txt";

        private static string F(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string Where(ESPNET.Port port)
        {
            return port.Owner.Model + " / " + port.Owner.Label + " (id " + port.Owner.ElementId + ")";
        }

        public static string Write(ESPRUN run, ESPSETTINGS settings, string title)
        {
            var sb = new StringBuilder();

            sb.AppendLine("ATKINSMEC - ESP");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine(title);
            sb.AppendLine();

            sb.AppendLine("=== Settings ===");
            sb.AppendLine("joint tolerance between models " + F(settings.ToleranceMm, "0.#") + " mm, margin " +
                          F(settings.MarginPercent, "0.#") + " %, air " + F(settings.Density, "0.###") +
                          " kg/m3, roughness " + F(settings.RoughnessMm, "0.###") + " mm (flex " +
                          F(settings.FlexRoughnessMm, "0.###") + " mm), terminal default " +
                          F(settings.TerminalDropPa, "0.#") + " Pa");
            sb.AppendLine(settings.TableIsBuiltIn()
                ? "coefficients: the BUILT-IN rough table"
                : "coefficients: edited in the window");
            sb.AppendLine(settings.Families.Count + " family value(s) typed in");
            sb.AppendLine();

            sb.AppendLine("=== Models read ===");
            foreach (string line in run.Models)
                sb.AppendLine("  " + line);
            sb.AppendLine("  " + run.Parts + " duct element(s) in all");
            sb.AppendLine();

            sb.AppendLine("=== Joints made between models: " + run.Bridges.Count + " ===");
            foreach (ESPNET.Bridge b in run.Bridges)
            {
                sb.AppendLine("  " + F(b.Gap * 1000, "0") + " mm  " + Where(b.A) + "  <->  " + Where(b.B));
            }
            sb.AppendLine();

            sb.AppendLine("=== Near misses - facing ends in different models, beyond the tolerance: " +
                          run.NearMisses.Count + " ===");
            foreach (ESPNET.Bridge b in run.NearMisses.Take(30))
            {
                sb.AppendLine("  " + F(b.Gap * 1000, "0") + " mm  " + Where(b.A) + "  <->  " + Where(b.B));
            }
            sb.AppendLine();

            foreach (string missing in run.Missing)
                sb.AppendLine("NOT FOUND: " + missing);

            foreach (ESPNET.Result result in run.Results)
            {
                sb.AppendLine("=== " + result.Unit.Model + " / " + result.Unit.Label +
                              " (id " + result.Unit.ElementId + ") ===");
                sb.AppendLine("  worst outlet " + F(result.OutSide, "0.0") + " Pa, worst inlet " +
                              F(result.InSide, "0.0") + " Pa, ESP " + F(result.Esp, "0.0") +
                              " Pa, with margin " + F(result.EspWithMargin, "0.0") + " Pa");

                double? inModel;

                if (run.InModel.TryGetValue(result.Unit.Key, out inModel) && inModel.HasValue)
                    sb.AppendLine("  '" + settings.Parameter + "' on the unit now: " + F(inModel.Value, "0.#"));

                foreach (ESPNET.Run r in result.Runs)
                {
                    sb.AppendLine();
                    sb.AppendLine("  -- " + (r.System.Length == 0 ? "?" : r.System) + " port " +
                                  r.Start.ConnectorId + ", air " + (r.Converging ? "IN" : "OUT") +
                                  (r.Direction.Length == 0 ? " (taken from the system)" : string.Empty));

                    if (!r.Connected)
                    {
                        foreach (string note in r.Notes)
                            sb.AppendLine("     " + note);

                        continue;
                    }

                    sb.AppendLine("     " + r.Terminals + " terminal(s), " + r.Equipment + " other unit(s), " +
                                  r.DeadEnds + " dead end(s), " + r.Bridges + " joint(s) to other models, " +
                                  r.Loops + " loop(s)");
                    sb.AppendLine("     flow at the unit " + F(r.FlowAtUnit * 1000, "0") +
                                  " L/s, at the terminals " + F(r.FlowAtEnds * 1000, "0") + " L/s");
                    sb.AppendLine("     index " + F(r.Index, "0.0") + " Pa, ending at " + r.EndsAt);
                    sb.AppendLine("     " + r.Flagged + " item(s) on this network from defaults, " +
                                  r.FlaggedOnIndex + " of them on the index run");

                    foreach (string note in r.Notes)
                        sb.AppendLine("     NOTE " + note);

                    sb.AppendLine("     what | size | L/s | m/s | m | Pa/m | C | Pa | running | from");

                    foreach (ESPNET.Step s in r.Steps)
                    {
                        sb.AppendLine("     " + (s.Flagged ? "! " : "  ") + s.What + " | " + s.Size + " | " +
                                      F(s.Flow * 1000, "0") + " | " + F(s.Velocity, "0.00") + " | " +
                                      F(s.Length, "0.00") + " | " + F(s.PerMetre, "0.000") + " | " +
                                      F(s.Coefficient, "0.00") + " | " + F(s.Loss, "0.0") + " | " +
                                      F(s.Running, "0.0") + " | " + s.Source + " | " + s.Part.Model + " / " +
                                      s.Part.Label + " (id " + s.Part.ElementId + ")");
                    }
                }

                sb.AppendLine();
            }

            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), FileName);

            try
            {
                File.WriteAllText(path, sb.ToString());
            }
            catch
            {
                return string.Empty;
            }

            return path;
        }
    }
}
