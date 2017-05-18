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
        private MarkerRecorder markerRecorder;
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

            contextSwitchRecorder = new ContextSwitchRecorder();
            contextSwitchRecorder.TargetProcess = targetProcess;
            contextSwitchRecorder.Start();

            markerRecorder = new MarkerRecorder();
            markerRecorder.Start();

            return true;
        }

        public void Stop()
        {
            markerRecorder.Stop();
            markerRecorder = null;
            contextSwitchRecorder.Stop();
            contextSwitchRecorder = null;
            targetProcess = null;
        }

        public List<Marker> GetMarkerSpan(DateTime startTime, DateTime lastTime)
        {
            return markerRecorder.GetMarkerSpan(startTime, lastTime);
        }

        public List<ContextSwitch> GetContextSwitchSpan(DateTime startTime, DateTime lastTime)
        {
            return contextSwitchRecorder.GetContextSwitchSpan(startTime, lastTime);
        }
    }
}
