using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace ATKINSMEC
{
    // Can the central model actually be reached right now?
    //
    // "Is the internet up" is the wrong question for half of these
    // models. A file-based central lives on a server share and needs the
    // LAN, not the internet; a cloud model needs the internet and does
    // not care about the share. So the probe follows what the model
    // actually syncs to, and says which it tested.
    //
    // Everything here is BOUNDED. An unreachable UNC path can take the
    // best part of a minute to admit it, and this runs on Revit's own
    // thread - a probe that blocks is a frozen Revit. Nothing here may
    // outstay its timeout.
    //
    // No Revit in this file, so it can be exercised with no model open.
    public class SYNCNET
    {
        public const string DefaultHost = "autodesk.com";
        public const int Port = 443;
        public const int TimeoutMs = 4000;

        public class Answer
        {
            public bool Reachable;
            public string Why = string.Empty;

            // True when the probe ran out of time rather than being
            // refused - worth saying, because the two look the same to a
            // person and mean different things
            public bool TimedOut;
        }

        // ==================================================
        // A cloud model: is the internet there?
        // ==================================================

        public static Answer Internet(string host, int timeoutMs)
        {
            string wanted = string.IsNullOrWhiteSpace(host)
                ? DefaultHost
                : host.Trim();

            int allowed = timeoutMs > 0 ? timeoutMs : TimeoutMs;

            var answer = new Answer();

            bool refused = false;

            bool finished = Bounded(
                delegate
                {
                    try
                    {
                        using (var client = new TcpClient())
                        {
                            IAsyncResult pending = client.BeginConnect(
                                wanted, Port, null, null);

                            if (!pending.AsyncWaitHandle.WaitOne(allowed))
                                return false;

                            client.EndConnect(pending);

                            return true;
                        }
                    }
                    catch
                    {
                        // No DNS, no route, refused - all of them mean
                        // the same thing here: not now
                        refused = true;

                        return false;
                    }
                },
                allowed + 1000,
                out answer.Reachable);

            if (answer.Reachable)
            {
                answer.Why = "reached " + wanted;

                return answer;
            }

            answer.TimedOut = !finished || !refused;

            answer.Why = finished
                ? (refused
                    ? "could not reach " + wanted
                    : "no answer from " + wanted)
                : "the connection test did not finish in time";

            return answer;
        }

        // ==================================================
        // A file-based central: is the share there?
        // ==================================================

        public static Answer Share(string centralPath)
        {
            var answer = new Answer();

            if (string.IsNullOrWhiteSpace(centralPath))
            {
                answer.Why = "no central model path to test";

                return answer;
            }

            string folder;

            try
            {
                folder = Path.GetDirectoryName(centralPath);
            }
            catch
            {
                answer.Why = "the central model path cannot be read";

                return answer;
            }

            if (string.IsNullOrEmpty(folder))
            {
                answer.Why = "no folder in the central model path";

                return answer;
            }

            bool finished = Bounded(
                delegate
                {
                    try
                    {
                        // The file itself, not just the folder: a share
                        // can be mounted while the model is gone
                        return File.Exists(centralPath)
                               || Directory.Exists(folder);
                    }
                    catch
                    {
                        return false;
                    }
                },
                TimeoutMs,
                out answer.Reachable);

            if (answer.Reachable)
            {
                answer.Why = "reached the central model";

                return answer;
            }

            answer.TimedOut = !finished;

            answer.Why = finished
                ? "cannot see " + folder
                : "no answer from " + folder + " in time";

            return answer;
        }

        // ==================================================

        // Runs the test on its own thread and gives up when the time is
        // gone. A thread left running is harmless - it is a background
        // thread and cannot hold Revit open - but its answer is ignored.
        private static bool Bounded(
            Func<bool> test, int timeoutMs, out bool result)
        {
            bool answer = false;

            var done = new ManualResetEvent(false);

            var worker = new Thread(delegate ()
            {
                try
                {
                    answer = test();
                }
                catch
                {
                    answer = false;
                }

                try
                {
                    done.Set();
                }
                catch
                {
                    // the waiter gave up and disposed it; nothing to do
                }
            });

            worker.IsBackground = true;
            worker.Start();

            bool finished = done.WaitOne(timeoutMs);

            result = finished && answer;

            return finished;
        }
    }
}
