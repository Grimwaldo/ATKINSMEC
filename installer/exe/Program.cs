using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ATKINSMEC.Setup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool silent = Has(args, "/silent", "-silent", "/s");
            bool uninstall = Has(args, "/uninstall", "-uninstall", "/u");

            if (silent)
                return Headless(uninstall);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new SetupForm());

            return 0;
        }

        private static bool Has(string[] args, params string[] flags)
        {
            foreach (string arg in args)
            {
                foreach (string flag in flags)
                {
                    if (string.Equals(arg, flag,
                            StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        // For rolling out across an office without anyone clicking:
        //     ATKINSMEC-Setup.exe /silent
        //     ATKINSMEC-Setup.exe /silent /uninstall
        private static int Headless(bool uninstall)
        {
            try
            {
                List<string> running = Installer.RunningRevits();

                if (running.Count > 0)
                {
                    Console.Error.WriteLine(
                        "Revit is running; close it first.");

                    return 2;
                }

                int touched = 0;

                foreach (string version in Payload.Versions())
                {
                    if (uninstall)
                    {
                        if (Installer.Uninstall(version) > 0)
                            touched++;
                    }
                    else if (Installer.RevitPresent(version))
                    {
                        Installer.Install(version);

                        touched++;
                    }
                }

                if (touched == 0 && !uninstall)
                {
                    Console.Error.WriteLine(
                        "No Revit installation was found.");

                    return 3;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);

                return 1;
            }
        }
    }
}
