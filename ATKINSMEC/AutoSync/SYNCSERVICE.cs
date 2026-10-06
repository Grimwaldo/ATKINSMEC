using Autodesk.Revit.UI;
using System;
using System.Timers;

namespace ATKINSMEC
{
    // The clock. Holds no Revit API call of its own - a timer tick is
    // not an API context, exactly like a form's button handler, so all
    // this does is raise the external event and let SYNCEVENT do the
    // work on Revit's own thread.
    //
    // Static because it outlives every window: switching AutoSync on and
    // then closing the settings window must not stop it.
    public class SYNCSERVICE
    {
        private static readonly object Gate = new object();

        private static Timer _timer;
        private static ExternalEvent _event;
        private static SYNCEVENT _handler;

        // Set by SYNCEVENT while a run is in progress. A sync of a big
        // model can outlast the interval, and ticks must not stack up
        // behind it.
        public static bool Busy { get; set; }

        public static bool On { get; private set; }

        public static DateTime NextRun { get; private set; }

        // Created once, when Revit is up. ExternalEvent.Create must
        // happen on Revit's thread, so this is called from
        // ApplicationInitialized rather than from a form.
        public static void Prepare()
        {
            lock (Gate)
            {
                if (_event != null)
                    return;

                _handler = new SYNCEVENT();
                _event = ExternalEvent.Create(_handler);
            }
        }

        public static bool Ready
        {
            get
            {
                lock (Gate)
                {
                    return _event != null;
                }
            }
        }

        public static void Start(int minutes)
        {
            lock (Gate)
            {
                if (_event == null)
                    return;

                int wanted = SYNCSETTINGS.Floor(minutes);

                Stop(false);

                _timer = new Timer(wanted * 60.0 * 1000.0);
                _timer.AutoReset = true;
                _timer.Elapsed += Tick;
                _timer.Start();

                On = true;
                NextRun = DateTime.Now.AddMinutes(wanted);
            }

            SYNCLOG.Add(SYNCLOG.What.Note, string.Empty,
                "AutoSync on, every " + SYNCSETTINGS.Floor(minutes) +
                " minute(s)");
        }

        public static void Stop()
        {
            Stop(true);
        }

        private static void Stop(bool say)
        {
            lock (Gate)
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Elapsed -= Tick;
                    _timer.Dispose();
                    _timer = null;
                }

                On = false;
                NextRun = DateTime.MinValue;
            }

            if (say)
                SYNCLOG.Add(SYNCLOG.What.Note, string.Empty, "AutoSync off");
        }

        // The interval changed while it was running
        public static void Restart(int minutes)
        {
            if (!On)
                return;

            Start(minutes);
        }

        private static void Tick(object sender, ElapsedEventArgs e)
        {
            lock (Gate)
            {
                NextRun = DateTime.Now.AddMinutes(
                    SYNCSETTINGS.Floor(SYNCSETTINGS.Minutes));
            }

            if (Busy)
            {
                // The last run is still going. Saying so beats silence:
                // it explains a gap in the log.
                SYNCLOG.Add(SYNCLOG.What.Skipped, string.Empty,
                    "the previous run had not finished");

                return;
            }

            Raise(SYNCEVENT.Why.Timer);
        }

        // Also used by the Sync now button
        public static void Raise(SYNCEVENT.Why why)
        {
            ExternalEvent raised;

            lock (Gate)
            {
                if (_event == null || _handler == null)
                    return;

                _handler.Reason = why;

                raised = _event;
            }

            try
            {
                // Safe from any thread, and it waits for Revit to be
                // idle - which is what keeps this from interrupting
                // somebody mid-command
                raised.Raise();
            }
            catch (Exception ex)
            {
                SYNCLOG.Add(SYNCLOG.What.Failed, string.Empty,
                    "could not ask Revit to run: " + ex.Message);
            }
        }

        // Revit is closing
        public static void Shutdown()
        {
            lock (Gate)
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Elapsed -= Tick;
                    _timer.Dispose();
                    _timer = null;
                }

                On = false;

                _event = null;
                _handler = null;
            }
        }
    }
}
