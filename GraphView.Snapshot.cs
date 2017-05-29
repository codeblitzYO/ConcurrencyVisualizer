using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Diagnostics;

namespace ETW
{
    public partial class GraphView : UserControl
    {
        struct Span<T>
        {
            public T enter;
            public T leave;
        }

        class Snapshot
        {
            public class Thread
            {
                public ProcessThread ProcessThread { get; private set; }
                public List<List<Span<Marker>>> MarkerSpan { get; private set; }
                public List<Span<ContextSwitch>> ThreadSpan { get; private set; }
                public int Line { get; private set; }

                public Thread(ProcessThread processThread, List<List<Span<Marker>>> markersSpans, List<Span<ContextSwitch>> threadSpans, int line)
                {
                    ProcessThread = processThread;
                    MarkerSpan = markersSpans;
                    ThreadSpan = threadSpans;
                    Line = line;
                }
            }
            public class Processor
            {
                public int Index { get; private set; }
                public List<Span<ContextSwitch>> Spans { get; private set; }

                public Processor(int index, List<Span<ContextSwitch>> spans)
                {
                    Index = index;
                    Spans = spans;
                }
            }

            public Thread[] threads = new Thread[0];
            public Processor[] processors = new Processor[0];
            public DateTime startTime;
            public DateTime lastTime;

            public void Refresh(Sampler dataSource, DateTime startTime, DateTime lastTime)
            {
                this.startTime = startTime;
                this.lastTime = lastTime;

                var contextSwitchSpan = dataSource.GetContextSwitchSpan(startTime, lastTime);

                processors = new Processor[Environment.ProcessorCount];
                for (var i = 0; i < Environment.ProcessorCount; ++i)
                {
                    processors[i] = new Processor(i, MakeProcessorSpan(i, contextSwitchSpan));                    
                }

                int line = 0;
                threads = new Thread[dataSource.TargetProcess.Threads.Count];
                for (var i = 0; i < dataSource.TargetProcess.Threads.Count; ++i)
                {
                    var processThread = dataSource.TargetProcess.Threads[i];

                    var markerSpan = new List<List<Span<Marker>>>();
                    for (int k = 0; k < MarkerRecorder.ProvidersGuid.Length; ++k)
                    {
                        var markers = dataSource.GetMarkerSpan(processThread.Id, k, startTime, lastTime);
                        if (markers != null && markers.Count > 0)
                        {
                            markerSpan.Add(MakeMarkerSpan(markers));
                        }
                    }

                    threads[i] = new Thread(processThread, markerSpan, MakeThreadSpan(processThread.Id, contextSwitchSpan), line);
                    line += markerSpan.Count + 1;
                }
            }

            private List<Span<ContextSwitch>> MakeProcessorSpan(int processorId, List<ContextSwitch> contextSwitchSpan)
            {
                var spans = new List<Span<ContextSwitch>>();
                var enterCs = new ContextSwitch();

                foreach (var cs in contextSwitchSpan)
                {
                    if (cs.processor != processorId)
                    {
                        continue;
                    }

                    switch (cs.action)
                    {
                        case ContextSwitch.ActionType.Enter:
                            enterCs = cs;
                            break;
                        case ContextSwitch.ActionType.Leave:
                            if (enterCs.action == ContextSwitch.ActionType.Enter)
                            {
                                spans.Add(new Span<ContextSwitch>() { enter = enterCs, leave = cs });
                                enterCs.action = ContextSwitch.ActionType.None;
                            }
                            break;
                        default:
                            break;
                    }
                }

                return spans;
            }

            private List<Span<Marker>> MakeMarkerSpan(List<Marker> markers)
            {
                var spans = new List<Span<Marker>>();
                for (var i = 0; i < markers.Count; ++i)
                {
                    var marker = markers[i];

                    switch (marker.e)
                    {
                        case Marker.Event.EnterSpan:
                            for (var j = i + 1; j < markers.Count; ++j)
                            {
                                var marker2 = markers[j];
                                if (marker2.e == Marker.Event.LeaveSpan && marker.id == marker2.id)
                                {
                                    spans.Add(new Span<Marker>() { enter=marker, leave=marker2 });
                                }
                            }
                            break;
                        case Marker.Event.Flag:
                        case Marker.Event.Message:
                            spans.Add(new Span<Marker>() { enter = marker, leave = marker });
                            break;
                        default:
                            break;
                    }
                }

                return spans;
            }

            private List<Span<ContextSwitch>> MakeThreadSpan(int threadId, List<ContextSwitch> contextSwitches)
            {
                var spans = new List<Span<ContextSwitch>>();
                ContextSwitch enterCs = new ContextSwitch();

                foreach (var cs in contextSwitches)
                {
                    if (cs.oldThread == threadId)
                    {
                        if (enterCs.newThread == threadId)
                        {
                            spans.Add(new Span<ContextSwitch>() { enter = enterCs, leave = cs });
                            enterCs.newThread = 0;
                        }
                    }
                    if (cs.newThread == threadId)
                    {
                        enterCs = cs;
                    }
                }

                return spans;
            }
        }
    }
}
