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
    public struct ContextSwitch
    {
        public int processor;
        public int newThread;
        public int oldThread;
        public DateTime timestamp;
    }
    public struct Stackwalk
    {
        public int thread;
        public ulong[] pc;
        public DateTime timestamp;
    }

    class ContextSwitchRecorder : EventRecorder
    {
        const int RecordCountMax = 100000;

        public static readonly Guid DefaultGuid = new Guid("1AC26ADB-2AA9-482F-AFED-3ADA43EA46DD");
            
        private List<ContextSwitch> csRecord = new List<ContextSwitch>();
        private List<Stackwalk> swRecord = new List<Stackwalk>();
        private object csRecordLock = new object();
        private object swRecordLock = new object();


        public override bool IsKernel { get { return true; } }

        public Process TargetProcess { get; set; }

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

            lock (swRecordLock)
            {
                swRecord.Add(new Stackwalk()
                {
                    thread = data.ThreadID,
                    pc = pc,
                    timestamp = data.TimeStamp
                });

                if (swRecord.Count > RecordCountMax)
                {
                    swRecord.RemoveRange(0, RecordCountMax / 10);
                }
            }
        }

        private void Kernel_ThreadCSwitch(Microsoft.Diagnostics.Tracing.Parsers.Kernel.CSwitchTraceData data)
        {
            if (TargetProcess != null && data.ProcessID != TargetProcess.Id)
            {
                return;
            }

            lock (csRecordLock)
            {
                csRecord.Add(new ContextSwitch()
                {
                    processor = data.ProcessorNumber,
                    oldThread = data.OldThreadID,
                    newThread = data.NewThreadID,
                    timestamp = data.TimeStamp
                });

                if (csRecord.Count > RecordCountMax)
                {
                    csRecord.RemoveRange(0, RecordCountMax / 10);
                }
            }
        }

        public List<ContextSwitch> GetContextSwitchSpan(DateTime startTime, DateTime lastTime)
        {
            lock (csRecordLock)
            {
                return csRecord.FindAll(e => e.timestamp >= startTime && e.timestamp < lastTime);
            }
        }

        public List<Stackwalk> GetStackwalkSpan(DateTime startTime, DateTime lastTime)
        {
            lock (swRecordLock)
            {
                return swRecord.FindAll(e => e.timestamp >= startTime && e.timestamp < lastTime);
            }
        }
    }
}
