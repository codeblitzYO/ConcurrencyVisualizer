using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace ETW
{
    public abstract class EventRecorder
    {
        private TraceEventSession etwSesion;
        private Thread runner;

        protected TraceEventSession Session
        {
            get { return etwSesion; }
        }

        public virtual bool IsKernel { get { return false; } }

        public virtual void Start(Guid guid)
        {
            var sessionName = IsKernel ? KernelTraceEventParser.KernelSessionName : "Session" + guid.ToString();

            etwSesion = new TraceEventSession(sessionName);
            etwSesion.StopOnDispose = true;

            InitializeProviders(guid);

            runner = new Thread(() =>
            {
                etwSesion.Source.Process();
            });
            runner.Start();
        }

        public virtual void Stop()
        {
            if (etwSesion != null)
            {
                etwSesion.Stop();
            }
            runner = null;
            etwSesion = null;
        }

        protected abstract void InitializeProviders(Guid guid);
    }
}
