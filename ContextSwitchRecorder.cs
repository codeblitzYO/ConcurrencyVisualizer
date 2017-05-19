using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;
using System.Diagnostics;

namespace ETW
{
    public struct ContextSwitch : IRecordData
    {
        public enum ActionType
        {
            None, Enter, Leave, Stay
        }

        public ActionType action;
        public int processor;
        public int newThread;
        public int oldThread;
        public DateTime timestamp;

        public DateTime Timestamp
        {
            get { return timestamp; }
            set { timestamp = value; }
        }
    }
    public struct Stackwalk : IRecordData
    {
        public int thread;
        public ulong[] pc;
        public DateTime timestamp;

        public DateTime Timestamp
        {
            get { return timestamp; }
            set { timestamp = value; }
        }
    }

    class ContextSwitchRecorder : EventRecorder
    {            
        private Record<ContextSwitch> csRecord = new Record<ContextSwitch>();
        private Record<Stackwalk> swRecord = new Record<Stackwalk>();


        public override string SessionName { get { return KernelTraceEventParser.KernelSessionName; } }

        public Process TargetProcess { get; set; }

        public ContextSwitchRecorder()
        {
        }

        protected override void InitializeProviders()
        {
            Session.EnableKernelProvider(KernelTraceEventParser.Keywords.ContextSwitch, KernelTraceEventParser.Keywords.ContextSwitch);

            Session.Source.Kernel.ThreadCSwitch += Kernel_ThreadCSwitch;
            Session.Source.Kernel.StackWalkStack += Kernel_StackWalkStack;
        }

        private void Kernel_StackWalkStack(Microsoft.Diagnostics.Tracing.Parsers.Kernel.StackWalkStackTraceData data)
        {
            if (TargetProcess != null && data.ProcessID != TargetProcess.Id)
            {
                return;
            }

            var pc = new ulong[data.FrameCount];
            for (int i = 0; i < data.FrameCount; ++i)
            {
                pc[i] = data.InstructionPointer(i);
            }

            swRecord.Append(new Stackwalk()
            {
                thread = data.ThreadID,
                pc = pc,
                timestamp = data.TimeStamp
            });
        }

        private void Kernel_ThreadCSwitch(Microsoft.Diagnostics.Tracing.Parsers.Kernel.CSwitchTraceData data)
        {
            if (TargetProcess == null)
            {
                return;
            }
            if (data.NewProcessID != TargetProcess.Id && data.OldProcessID != TargetProcess.Id)
            {
                return;
            }

            ContextSwitch.ActionType processAction = 
                (data.NewProcessID == data.OldProcessID)
                    ? ContextSwitch.ActionType.Stay
                    : (data.NewProcessID == TargetProcess.Id)
                        ? ContextSwitch.ActionType.Enter
                        : ContextSwitch.ActionType.Leave;      

            csRecord.Append(new ContextSwitch()
            {
                action = processAction,
                processor = data.ProcessorNumber,
                oldThread = (data.OldProcessID == TargetProcess.Id) ? data.OldThreadID : 0,
                newThread = (data.NewProcessID == TargetProcess.Id) ? data.NewThreadID : 0,
                timestamp = data.TimeStamp
            });
        }

        public List<ContextSwitch> GetContextSwitchSpan(DateTime startTime, DateTime lastTime)
        {
            return csRecord.GetSpan(startTime, lastTime);
        }

        public List<Stackwalk> GetStackwalkSpan(DateTime startTime, DateTime lastTime)
        {
            return swRecord.GetSpan(startTime, lastTime);
        }
    }
}
