using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace ETW
{
    public class Sampler
    {
        private Process targetProcess = null;
        private List<MarkerRecorder> markerRecorderArray = new List<MarkerRecorder>();
        private ContextSwitchRecorder contextSwitchRecorder;

        public Process TargetProcess { get { return targetProcess; } }


        public Sampler()
        {

        }

        public bool Start(string processName)
        {
            if (!string.IsNullOrEmpty(processName))
            {
                int processId;
                if (!int.TryParse(processName, out processId))
                {
                    processId = -1;
                }

                var allProcess = Process.GetProcesses();
                foreach (var process in allProcess)
                {
                    if (process.ProcessName.IndexOf(processName) == 0 || process.Id == processId)
                    {
                        targetProcess = process;
                    }
                }

                if (targetProcess == null)
                {
                    return false;
                }
            }

            var defaultGuid = new Guid("8d4925ab-505a-483b-a7e0-6f824a07a6f0");

            contextSwitchRecorder = new ContextSwitchRecorder(ContextSwitchRecorder.DefaultGuid);
            contextSwitchRecorder.TargetProcess = targetProcess;

            markerRecorderArray.Add(new MarkerRecorder(defaultGuid));
            markerRecorderArray.Add(new MarkerRecorder(new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece00")));
            markerRecorderArray.Add(new MarkerRecorder(new Guid("edbc9dc2-0c50-48e4-88df-65aa0d8ece01")));

            return true;
        }

        public void Stop()
        {
            foreach (var i in markerRecorderArray)
            {
                i.Stop();
            }
            markerRecorderArray.Clear();
            contextSwitchRecorder.Stop();
            contextSwitchRecorder = null;
            targetProcess = null;
        }

        public List<Marker> GetMarkerSpan(DateTime startTime, DateTime lastTime)
        {
            var allin = new List<Marker>();
            foreach (var i in markerRecorderArray)
            {
                allin.AddRange(i.GetMarkerSpan(startTime, lastTime));
            }
            allin.Sort((x, y) => { return x.timestamp.CompareTo(y.timestamp); });
            return allin;
        }

        public List<ContextSwitch> GetContextSwitchSpan(DateTime startTime, DateTime lastTime)
        {
            return contextSwitchRecorder.GetContextSwitchSpan(startTime, lastTime);
        }

        public DateTime GetLastestEventTime()
        {
            var time = new DateTime();
            foreach (var i in markerRecorderArray)
            {
                if (time < i.LastestEventTime)
                {
                    time = i.LastestEventTime;
                }
            }

            if (time < contextSwitchRecorder.LastestEventTime)
            {
                time = contextSwitchRecorder.LastestEventTime;
            }

            return time;
        }
    }
}
