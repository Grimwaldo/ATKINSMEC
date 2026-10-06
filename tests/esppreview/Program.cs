using ATKINSMEC;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Renders the REAL ESPForm with no Revit: a network built by hand through
// the real ESPNET (an AHU and its riser in a linked model, bridged to
// floor ductwork in this one, and two fan coils), calculated, shown on
// all three tabs, saved as PNGs. Then exports the same run through the
// real ESPEXPORT and reads the workbook back.
//
//   esppreview <folder> [scale]     scale 1.5 = a screen at 150%
internal static class Program
{
    private static long _id = 5000;

    private static ESPNET.Part Part(ESPNET.Kind kind, long link, string family, string model, string mark = "")
    {
        return new ESPNET.Part
        {
            What = kind, LinkId = link, ElementId = _id++, Family = family, Model = model, Mark = mark
        };
    }

    private static ESPNET.Port Port(ESPNET.Part part, double x, double y, double z,
        double dx, double dy, double dz, ESPNET.Shape shape, double a, double b = 0,
        string system = "", string direction = "")
    {
        var port = new ESPNET.Port
        {
            Owner = part, ConnectorId = part.Ports.Count,
            X = x, Y = y, Z = z, DX = dx, DY = dy, DZ = dz,
            Section = shape, System = system, Direction = direction
        };

        if (shape == ESPNET.Shape.Round) port.Diameter = a;
        else { port.Width = a; port.Height = b; }

        part.Ports.Add(port);
        return port;
    }

    private static ESPNET.Part Duct(long link, string model, double x0, double y0, double z0,
        double x1, double y1, double z1, ESPNET.Shape shape, double a, double b = 0)
    {
        ESPNET.Part duct = Part(ESPNET.Kind.Duct, link,
            shape == ESPNET.Shape.Round ? "Round Duct : Taps" : "Rectangular Duct : Mitered Elbows / Taps", model);

        duct.Section = shape;
        if (shape == ESPNET.Shape.Round) duct.Diameter = a; else { duct.Width = a; duct.Height = b; }

        double lx = x1 - x0, ly = y1 - y0, lz = z1 - z0, len = Math.Sqrt(lx * lx + ly * ly + lz * lz);

        ESPNET.Port p0 = Port(duct, x0, y0, z0, -lx / len, -ly / len, -lz / len, shape, a, b);
        ESPNET.Port p1 = Port(duct, x1, y1, z1, lx / len, ly / len, lz / len, shape, a, b);
        p0.Along = 0;
        p1.Along = len;

        return duct;
    }

    private const string Here = "This model";
    private const string Tower = "5232535-ATR-TWR-ZZZ-3DM-HVC-000020.rvt : 1 : Tower A";

    private static ESPRUN Run(ESPSETTINGS settings)
    {
        var parts = new List<ESPNET.Part>();

        // ---- the AHU and its riser, in the linked model
        ESPNET.Part ahu = Part(ESPNET.Kind.Equipment, 11, "AHU - Modular : 3000 L/s", Tower, "AHU-L03-01");
        ESPNET.Port sup = Port(ahu, 0, 0, 0, 0, 0, 1, ESPNET.Shape.Rectangular, 0.8, 0.5, "Supply", "Out");
        ESPNET.Port ret = Port(ahu, 3, 0, 0, 0, 0, 1, ESPNET.Shape.Rectangular, 0.8, 0.5, "Return", "In");
        ESPNET.Port oa = Port(ahu, 6, 0, 0, 0, 0, 1, ESPNET.Shape.Rectangular, 0.6, 0.4, "Other", "In");
        sup.Flow = 1.2; ret.Flow = 1.0; oa.Flow = 0.4;

        ESPNET.Part riser = Duct(11, Tower, 0, 0, 0, 0, 0, 12, ESPNET.Shape.Rectangular, 0.8, 0.5);
        ESPNET.Join(sup, riser.Ports[0]);

        ESPNET.Part atten = Part(ESPNET.Kind.Accessory, 11, "Attenuator - Rectangular : 1200 long", Tower);
        ESPNET.Port at0 = Port(atten, 0, 0, 12, 0, 0, -1, ESPNET.Shape.Rectangular, 0.8, 0.5);
        ESPNET.Port at1 = Port(atten, 0, 0, 13.2, 0, 0, 1, ESPNET.Shape.Rectangular, 0.8, 0.5);
        ESPNET.Join(riser.Ports[1], at0);

        ESPNET.Part riser2 = Duct(11, Tower, 0, 0, 13.2, 0, 0, 16, ESPNET.Shape.Rectangular, 0.8, 0.5);
        ESPNET.Join(at1, riser2.Ports[0]);

        parts.AddRange(new[] { ahu, riser, atten, riser2 });

        // ---- the floor, in this model, starting 40 mm above the riser
        ESPNET.Part elbow = Part(ESPNET.Kind.Fitting, 0, "Rectangular Elbow - Radius : Standard", Here);
        elbow.PartType = "Elbow";
        ESPNET.Port e0 = Port(elbow, 0, 0, 16.04, 0, 0, -1, ESPNET.Shape.Rectangular, 0.8, 0.5);
        ESPNET.Port e1 = Port(elbow, 0, 0, 16.04, 1, 0, 0, ESPNET.Shape.Rectangular, 0.8, 0.5);

        ESPNET.Part main = Duct(0, Here, 0, 0, 16.04, 24, 0, 16.04, ESPNET.Shape.Rectangular, 0.8, 0.5);
        ESPNET.Join(e1, main.Ports[0]);

        ESPNET.Part fd = Part(ESPNET.Kind.Accessory, 0, "Fire Damper - Rectangular : 800x500", Here);
        ESPNET.Port fd0 = Port(fd, 24, 0, 16.04, -1, 0, 0, ESPNET.Shape.Rectangular, 0.8, 0.5);
        ESPNET.Port fd1 = Port(fd, 24, 0, 16.04, 1, 0, 0, ESPNET.Shape.Rectangular, 0.8, 0.5);
        ESPNET.Join(main.Ports[1], fd0);

        ESPNET.Part main2 = Duct(0, Here, 24, 0, 16.04, 40, 0, 16.04, ESPNET.Shape.Rectangular, 0.6, 0.4);
        ESPNET.Join(fd1, main2.Ports[0]);

        parts.AddRange(new[] { elbow, main, fd, main2 });

        // Six diffusers on taps along the two mains
        int n = 0;

        foreach (ESPNET.Part m in new[] { main, main2 })
        {
            for (int i = 1; i <= 3; i++)
            {
                double x = m.Ports[0].X + (m.Ports[1].X - m.Ports[0].X) * i / 4.0;

                ESPNET.Port curve = Port(m, x, 0, 16.04, 0, 1, 0, m.Section, m.Width, m.Height);
                curve.IsCurve = true;
                curve.Along = x - m.Ports[0].X;

                ESPNET.Part tap = Part(ESPNET.Kind.Fitting, 0, "Rectangular Takeoff : Bellmouth", Here);
                tap.PartType = "TapPerpendicular";
                ESPNET.Port t0 = Port(tap, x, 0, 16.04, 0, -1, 0, ESPNET.Shape.Round, 0.25);
                ESPNET.Port t1 = Port(tap, x, 0, 16.04, 0, 1, 0, ESPNET.Shape.Round, 0.25);
                ESPNET.Join(curve, t0);

                ESPNET.Part flex = Part(ESPNET.Kind.Flex, 0, "Flex Duct Round : Flex - Round", Here);
                flex.Section = ESPNET.Shape.Round; flex.Diameter = 0.25; flex.Length = 1.8;
                ESPNET.Port f0 = Port(flex, x, 0, 16.04, 0, -1, 0, ESPNET.Shape.Round, 0.25);
                ESPNET.Port f1 = Port(flex, x, 1.5, 15.6, 0, 1, 0, ESPNET.Shape.Round, 0.25);
                ESPNET.Join(t1, f0);

                ESPNET.Part diffuser = Part(ESPNET.Kind.Terminal, 0, "Supply Diffuser - Square : 600x600", Here);
                diffuser.Flow = 0.2;
                diffuser.PressureDrop = n % 3 == 0 ? 0 : 18;
                ESPNET.Join(f1, Port(diffuser, x, 1.5, 15.6, 0, -1, 0, ESPNET.Shape.Round, 0.25));

                parts.AddRange(new[] { tap, flex, diffuser });
                n++;
            }
        }

        // ---- return: a short run to two grilles, all in the linked model
        ESPNET.Part rr = Duct(11, Tower, 3, 0, 0, 3, 0, 4, ESPNET.Shape.Rectangular, 0.8, 0.5);
        ESPNET.Join(ret, rr.Ports[0]);
        ESPNET.Part rg = Part(ESPNET.Kind.Terminal, 11, "Return Grille : 1200x600", Tower);
        rg.Flow = 1.0;
        ESPNET.Join(rr.Ports[1], Port(rg, 3, 0, 4, 0, 0, -1, ESPNET.Shape.Rectangular, 0.8, 0.5));
        parts.AddRange(new[] { rr, rg });

        // ---- fresh air: drawn, but its louvre is 300 mm off - not joined
        ESPNET.Part fa = Duct(11, Tower, 6, 0, 0, 6, 0, 2.5, ESPNET.Shape.Rectangular, 0.6, 0.4);
        ESPNET.Join(oa, fa.Ports[0]);
        ESPNET.Part louvre = Part(ESPNET.Kind.Terminal, 0, "Louvre : 1000x600", Here);
        louvre.Flow = 0.4;
        Port(louvre, 6, 0, 2.8, 0, 0, -1, ESPNET.Shape.Rectangular, 0.6, 0.4);
        parts.AddRange(new[] { fa, louvre });

        // ---- two fan coils in this model
        ESPNET.Part[] fcus = new ESPNET.Part[2];

        for (int k = 0; k < 2; k++)
        {
            double y = 20 + k * 10;
            ESPNET.Part fcu = Part(ESPNET.Kind.Equipment, 0, "FCU - Ducted : FCU-05", Here, "FCU-L03-0" + (k + 1));
            ESPNET.Port fs = Port(fcu, 0, y, 3, 1, 0, 0, ESPNET.Shape.Rectangular, 0.5, 0.2, "Supply", "Out");
            ESPNET.Port fr = Port(fcu, -1, y, 3, -1, 0, 0, ESPNET.Shape.Rectangular, 0.5, 0.2, "Return", "In");
            fs.Flow = 0.25; fr.Flow = 0.25;

            ESPNET.Part fsd = Duct(0, Here, 0, y, 3, 6, y, 3, ESPNET.Shape.Rectangular, 0.5, 0.2);
            ESPNET.Join(fs, fsd.Ports[0]);
            ESPNET.Part slot = Part(ESPNET.Kind.Terminal, 0, "Linear Slot Diffuser : 2 slot 1500", Here);
            slot.Flow = 0.25;
            slot.PressureDrop = 22;
            ESPNET.Join(fsd.Ports[1], Port(slot, 6, y, 3, -1, 0, 0, ESPNET.Shape.Rectangular, 0.5, 0.2));

            ESPNET.Part frd = Duct(0, Here, -1, y, 3, -2.5, y, 3, ESPNET.Shape.Rectangular, 0.5, 0.2);
            ESPNET.Join(fr, frd.Ports[0]);
            ESPNET.Part grille = Part(ESPNET.Kind.Terminal, 0, "Return Grille : 600x300", Here);
            grille.Flow = 0.25;
            ESPNET.Join(frd.Ports[1], Port(grille, -2.5, y, 3, 1, 0, 0, ESPNET.Shape.Rectangular, 0.5, 0.2));

            parts.AddRange(new[] { fcu, fsd, slot, frd, grille });
            fcus[k] = fcu;
        }

        var run = new ESPRUN
        {
            TableBuiltIn = settings.TableIsBuiltIn(),
            Parts = parts.Count,
            Models = { Here + ": 18 duct, 6 flex, 8 fitting, 1 accessory, 9 terminal, 2 equipment",
                       Tower + ": 4 duct, 1 accessory, 1 terminal, 1 equipment" }
        };

        List<ESPNET.Bridge> misses;
        run.Bridges = ESPNET.BridgeModels(parts, settings.ToleranceMm / 1000.0, out misses);
        run.NearMisses = misses;

        foreach (ESPNET.Part unit in new[] { ahu, fcus[0], fcus[1] })
        {
            run.Results.Add(ESPNET.Calculate(unit, settings));
            run.InModel[unit.Key] = unit == fcus[0] ? 60.0 : (double?)null;
        }

        return run;
    }

    private static T Find<T>(Control root) where T : Control
    {
        foreach (Control c in root.Controls)
        {
            if (c is T)
                return (T)c;

            T inner = Find<T>(c);

            if (inner != null)
                return inner;
        }

        return null;
    }

    // Every control that sets a font OF ITS OWN, found before any font
    // changes - an inherited font must not be scaled again at each level
    private static void ScaleFonts(Control root, float by)
    {
        var own = new List<KeyValuePair<Control, Font>>();

        Action<Control> find = null;
        find = c =>
        {
            if (c.Parent == null || !c.Font.Equals(c.Parent.Font))
                own.Add(new KeyValuePair<Control, Font>(c, c.Font));

            foreach (Control child in c.Controls)
                find(child);
        };

        find(root);

        foreach (KeyValuePair<Control, Font> pair in own)
            pair.Key.Font = new Font(pair.Value.FontFamily, pair.Value.Size * by, pair.Value.Style);
    }

    // Does a window built the way ESPForm.Build builds itself actually
    // scale? The same steps, with a font 1.5 times the base: the child
    // must come out 1.5 times its size.
    private static void ProveAutoScale()
    {
        using (var probe = new Form())
        {
            var child = new Panel { Width = 100, Height = 40 };

            probe.SuspendLayout();
            probe.AutoScaleDimensions = new SizeF(7F, 15F);
            probe.AutoScaleMode = AutoScaleMode.Font;
            probe.Font = new Font("Segoe UI", 13.5F);
            probe.ClientSize = new Size(300, 200);
            probe.Controls.Add(child);
            probe.ResumeLayout(false);
            probe.PerformLayout();

            Console.WriteLine("  auto-scale probe: a 100x40 child came out " + child.Width + "x" + child.Height +
                              (child.Width > 120 ? "  - it scales (font-based, so not exactly 1.5)" : "  - DOES NOT SCALE"));
        }
    }

    private static void Save(Form form, string path)
    {
        Application.DoEvents();

        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(path);
        }

        Console.WriteLine("  " + Path.GetFileName(path) + "  " + form.Width + "x" + form.Height);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string folder = args.Length > 0 ? args[0] : "out";
        double scale = args.Length > 1 ? double.Parse(args[1], CultureInfo.InvariantCulture) : 1.0;

        Directory.CreateDirectory(folder);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        ProveAutoScale();

        var settings = new ESPSETTINGS();
        settings.Families["Attenuator - Rectangular : 1200 long"] = 45;

        ESPRUN run = Run(settings);

        var links = new List<ESPLINKROW>
        {
            new ESPLINKROW { Id = 11, Label = Tower },
            new ESPLINKROW { Id = 12, Label = "5232535-ATR-TWR-ZZZ-3DM-HVC-000020.rvt : 2 : Tower B" },
            new ESPLINKROW { Id = 13, Label = "5232535-ATR-POD-ZZZ-3DM-ARC-000010.rvt" }
        };

        var units = new List<ESPUNITROW>
        {
            new ESPUNITROW { Key = "0:1", Name = "FCU - Ducted : FCU-05", Mark = "FCU-L03-01", Model = Here, Ports = "Supply, Return", Picked = true },
            new ESPUNITROW { Key = "0:2", Name = "FCU - Ducted : FCU-05", Mark = "FCU-L03-02", Model = Here, Ports = "Supply, Return", Picked = true },
            new ESPUNITROW { Key = "0:3", Name = "FCU - Ducted : FCU-08", Mark = "FCU-L03-03", Model = Here, Ports = "Supply, Return" },
            new ESPUNITROW { Key = "0:4", Name = "Exhaust Fan - Inline : EF-2", Mark = "EF-L03-01", Model = Here, Ports = "Exhaust" },
            new ESPUNITROW { Key = "11:5", Name = "AHU - Modular : 3000 L/s", Mark = "AHU-L03-01", Model = Tower, Ports = "Supply, Return, Other air", Picked = true },
            new ESPUNITROW { Key = "11:6", Name = "AHU - Modular : 5000 L/s", Mark = "AHU-L04-01", Model = Tower, Ports = "Supply, Return, Other air" }
        };

        var form = new ESPForm(
            "5232535-ATR-POD-ZZZ-3DM-HVC-000050",
            links,
            units.Where(u => u.Picked).Select(u => u.Key).ToList(),
            ids => units,
            (l, k, s) => run,
            (v, p) => "Wrote the ESP to " + v.Count + " unit(s).",
            settings);

        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(20, 20);

        Console.WriteLine("  auto-scale base " + form.AutoScaleDimensions + ", measured here " + form.CurrentAutoScaleDimensions);

        // WHAT WINFORMS DOES AT 150%: every size times 1.5 and every font
        // drawn 1.5 times bigger. Changing only the font after the window
        // is built scales nothing else, so that is not a fair render.
        if (scale != 1.0)
        {
            ScaleFonts(form, (float)scale);
            form.Scale(new SizeF((float)scale, (float)scale));
        }

        form.Show();
        form.SetUnits(units);
        Find<CheckedListBox>(form).SetItemChecked(0, true);

        string tag = scale == 1.0 ? string.Empty : "-" + scale.ToString("0.0#", CultureInfo.InvariantCulture) + "x";

        form.ShowTab(0);
        Save(form, Path.Combine(folder, "esp-units" + tag + ".png"));

        form.ShowRun(run);
        form.ShowTab(1);
        Save(form, Path.Combine(folder, "esp-results" + tag + ".png"));

        form.ShowTab(2);
        Save(form, Path.Combine(folder, "esp-coefficients" + tag + ".png"));

        // The same run through the real export, read back
        string xlsx = Path.Combine(folder, "esp-export" + tag + ".xlsx");
        ESPEXPORT.Write(xlsx, run, settings, "preview");
        Console.WriteLine("  " + Path.GetFileName(xlsx) + "  " + new FileInfo(xlsx).Length + " bytes");

        // And the log, into the folder rather than onto the Desktop
        foreach (ESPNET.Result r in run.Results)
        {
            Console.WriteLine("  " + r.Unit.Mark + ": ESP " + r.Esp.ToString("0.0", CultureInfo.InvariantCulture) +
                              " Pa (+margin " + r.EspWithMargin.ToString("0.0", CultureInfo.InvariantCulture) + "), " +
                              string.Join(", ", r.Runs.Select(x => x.System + " " + (x.Connected ? x.Index.ToString("0.0", CultureInfo.InvariantCulture) : "not joined") +
                                                                     " [" + x.Terminals + " terminals, " + x.Bridges + " bridges, " + x.DeadEnds + " dead]")));
        }

        Console.WriteLine("  bridges " + run.Bridges.Count + ", near misses " + run.NearMisses.Count);

        form.Close();

        return 0;
    }
}
