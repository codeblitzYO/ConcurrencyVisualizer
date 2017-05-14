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
    public struct ContextSwitch
    {
        public int processor;
        public int newThread;
        public int oldThread;
        public DateTime timestamp;
    }

    class ContextSwitchRecorder : EventRecorder
    {
        const int RecordCountMax = 100000;

        public static readonly Guid DefaultGuid = new Guid("1AC26ADB-2AA9-482F-AFED-3ADA43EA46DD");
            
        private List<ContextSwitch> spanRecord = new List<ContextSwitch>();

        public override bool IsKernel { get { return true; } }

        public ContextSwitchRecorder()
        {
        }
        public ContextSwitchRecorder(Guid guid)
        {
            Start(guid);
        }
        
        public override void Stop()
        {
            base.Stop();
        }

        protected override void InitializeProviders(Guid guid)
        {
            Session.EnableKernelProvider(KernelTraceEventParser.Keywords.ContextSwitch);

            Session.Source.Kernel.ThreadCSwitch += Kernel_ThreadCSwitch;
        }

        private void Kernel_ThreadCSwitch(Microsoft.Diagnostics.Tracing.Parsers.Kernel.CSwitchTraceData obj)
        {
            Console.WriteLine(obj);
        }
    }
}
