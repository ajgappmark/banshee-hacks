//
// Log.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2005-2007 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Text;
using System.Collections.Generic;

namespace Hyena
{
    internal delegate void LogNotifyHandler (LogNotifyArgs args);

    internal class LogNotifyArgs : EventArgs
    {
        private LogEntry entry;

        internal LogNotifyArgs (LogEntry entry)
        {
            this.entry = entry;
        }

        internal LogEntry Entry {
            get { return entry; }
        }
    }

    internal enum LogEntryType
    {
        Debug,
        Warning,
        Error,
        Information
    }

    internal class LogEntry
    {
        private LogEntryType type;
        private string message;
        private string details;
        private DateTime timestamp;

        internal LogEntry (LogEntryType type, string message, string details)
        {
            this.type = type;
            this.message = message;
            this.details = details;
            this.timestamp = DateTime.Now;
        }

        internal LogEntryType Type {
            get { return type; }
        }

        internal string Message {
            get { return message; }
        }

        internal string Details {
            get { return details; }
        }

        internal DateTime TimeStamp {
            get { return timestamp; }
        }
    }

    internal static class Log
    {
        internal static event LogNotifyHandler Notify;

        private static Dictionary<uint, DateTime> timers = new Dictionary<uint, DateTime> ();
        private static uint next_timer_id = 1;

        private static bool debugging = false;
        internal static bool Debugging {
            get { return debugging; }
            set { debugging = value; }
        }

        internal static void Commit (LogEntryType type, string message, string details, bool showUser)
        {
            if (type == LogEntryType.Debug && !Debugging) {
                return;
            }

            if (type != LogEntryType.Information || (type == LogEntryType.Information && !showUser)) {
                switch (type) {
                    case LogEntryType.Error: ConsoleCrayon.ForegroundColor = ConsoleColor.Red; break;
                    case LogEntryType.Warning: ConsoleCrayon.ForegroundColor = ConsoleColor.Yellow; break;
                    case LogEntryType.Information: ConsoleCrayon.ForegroundColor = ConsoleColor.Green; break;
                    case LogEntryType.Debug: ConsoleCrayon.ForegroundColor = ConsoleColor.Blue; break;
                }

                Console.Write ("[{0} {1:00}:{2:00}:{3:00}.{4:000}]", TypeString (type), DateTime.Now.Hour,
                    DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);

                ConsoleCrayon.ResetColor ();

                if (details != null) {
                    Console.WriteLine (" {0} - {1}", message, details);
                } else {
                    Console.WriteLine (" {0}", message);
                }
            }

            if (showUser) {
                OnNotify (new LogEntry (type, message, details));
            }
        }

        private static string TypeString (LogEntryType type)
        {
            switch (type) {
                case LogEntryType.Debug:         return "Debug";
                case LogEntryType.Warning:       return "Warn ";
                case LogEntryType.Error:         return "Error";
                case LogEntryType.Information:   return "Info ";
            }
            return null;
        }

        private static void OnNotify (LogEntry entry)
        {
            LogNotifyHandler handler = Notify;
            if (handler != null) {
                handler (new LogNotifyArgs (entry));
            }
        }

        #region Timer Methods

        internal static uint DebugTimerStart (string message)
        {
            return TimerStart (message, false);
        }

        internal static uint InformationTimerStart (string message)
        {
            return TimerStart (message, true);
        }

        private static uint TimerStart (string message, bool isInfo)
        {
            if (!Debugging && !isInfo) {
                return 0;
            }

            if (isInfo) {
                Information (message);
            } else {
                Debug (message);
            }

            return TimerStart (isInfo);
        }

        internal static uint DebugTimerStart ()
        {
            return TimerStart (false);
        }

        internal static uint InformationTimerStart ()
        {
            return TimerStart (true);
        }

        private static uint TimerStart (bool isInfo)
        {
            if (!Debugging && !isInfo) {
                return 0;
            }

            uint timer_id = next_timer_id++;
            timers.Add (timer_id, DateTime.Now);
            return timer_id;
        }

        internal static void DebugTimerPrint (uint id)
        {
            if (!Debugging) {
                return;
            }

            TimerPrint (id, "Operation duration: {0}", false);
        }

        internal static void DebugTimerPrint (uint id, string message)
        {
            if (!Debugging) {
                return;
            }

            TimerPrint (id, message, false);
        }

        internal static void InformationTimerPrint (uint id)
        {
            TimerPrint (id, "Operation duration: {0}", true);
        }

        internal static void InformationTimerPrint (uint id, string message)
        {
            TimerPrint (id, message, true);
        }

        private static void TimerPrint (uint id, string message, bool isInfo)
        {
            if (!Debugging && !isInfo) {
                return;
            }

            DateTime finish = DateTime.Now;

            if (!timers.ContainsKey (id)) {
                return;
            }

            TimeSpan duration = finish - timers[id];
            string d_message;
            if (duration.TotalSeconds < 60) {
                d_message = String.Format ("{0}s", duration.TotalSeconds);
            } else {
                d_message = duration.ToString ();
            }

            if (isInfo) {
                InformationFormat (message, d_message);
            } else {
                DebugFormat (message, d_message);
            }
        }

        #endregion

        #region Public Debug Methods

        internal static void Debug (string message, string details)
        {
            if (Debugging) {
                Commit (LogEntryType.Debug, message, details, false);
            }
        }

        internal static void Debug (string message)
        {
            if (Debugging) {
                Debug (message, null);
            }
        }

        internal static void DebugFormat (string format, params object [] args)
        {
            if (Debugging) {
                Debug (String.Format (format, args));
            }
        }

        #endregion

        #region Public Information Methods

        internal static void Information (string message)
        {
            Information (message, null);
        }

        internal static void Information (string message, string details)
        {
            Information (message, details, false);
        }

        internal static void Information (string message, string details, bool showUser)
        {
            Commit (LogEntryType.Information, message, details, showUser);
        }

        internal static void Information (string message, bool showUser)
        {
            Information (message, null, showUser);
        }

        internal static void InformationFormat (string format, params object [] args)
        {
            Information (String.Format (format, args));
        }

        #endregion

        #region Public Warning Methods

        internal static void Warning (string message)
        {
            Warning (message, null);
        }

        internal static void Warning (string message, string details)
        {
            Warning (message, details, false);
        }

        internal static void Warning (string message, string details, bool showUser)
        {
            Commit (LogEntryType.Warning, message, details, showUser);
        }

        internal static void Warning (string message, bool showUser)
        {
            Warning (message, null, showUser);
        }

        internal static void WarningFormat (string format, params object [] args)
        {
            Warning (String.Format (format, args));
        }

        #endregion

        #region Public Error Methods

        internal static void Error (string message)
        {
            Error (message, null);
        }

        internal static void Error (string message, string details)
        {
            Error (message, details, false);
        }

        internal static void Error (string message, string details, bool showUser)
        {
            Commit (LogEntryType.Error, message, details, showUser);
        }

        internal static void Error (string message, bool showUser)
        {
            Error (message, null, showUser);
        }

        internal static void ErrorFormat (string format, params object [] args)
        {
            Error (String.Format (format, args));
        }

        #endregion

        #region Public Exception Methods

        internal static void Exception (Exception e)
        {
            Exception (null, e);
        }

        internal static void Exception (string message, Exception e)
        {
            Stack<Exception> exception_chain = new Stack<Exception> ();
            StringBuilder builder = new StringBuilder ();

            while (e != null) {
                exception_chain.Push (e);
                e = e.InnerException;
            }

            while (exception_chain.Count > 0) {
                e = exception_chain.Pop ();
                builder.AppendFormat ("{0} (in `{1}')", e.Message, e.Source).AppendLine ();
                builder.Append (e.StackTrace);
                if (exception_chain.Count > 0) {
                    builder.AppendLine ();
                }
            }

            // FIXME: We should save these to an actual log file
            Log.Warning (message ?? "Caught an exception", builder.ToString (), false);
        }

        #endregion
    }
}
