using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace ETW
{
    public partial class GraphView : UserControl
    {
        class SpanGather
        {
            public Snapshot.Thread Thread { get; private set; }
            public List<Span<Marker>> Markers { get; private set; }
            public List<Span<ContextSwitch>> ThreadUsing { get; private set; }

            public DateTime StartTime { get; private set; }
            public DateTime LastTime { get; private set; }
            public TimeSpan Duration { get { return LastTime - StartTime; } }

            public string Name { get; private set; }

            SpanGather()
            {
            }

            static public SpanGather Gather(Snapshot snapshot, int line, DateTime startTime, DateTime lastTime)
            {
                if (snapshot.threads.Length == 0)
                {
                    return null;
                }

                Snapshot.Thread thread = snapshot.threads[0];
                foreach (var i in snapshot.threads)
                {
                    if (i.Line > line) break;
                    thread = i;
                }

                var spanData = new SpanGather();
                spanData.Thread = thread;
                spanData.Markers = new List<Span<Marker>>();
                spanData.ThreadUsing = new List<Span<ContextSwitch>>();

                var providerIndex = line - thread.Line - 1;
                if (providerIndex >= 0 && providerIndex < thread.MarkerSpan.Count)
                {
                    spanData.Markers = spanData.GatherSpan(thread.MarkerSpan[providerIndex], startTime, lastTime);
                }
                else
                {
                    spanData.ThreadUsing = spanData.GatherSpan(thread.ThreadSpan, startTime, lastTime);
                }

                if (spanData.Markers.Count == 0 && spanData.ThreadUsing.Count == 0)
                {
                    return null;
                }
                return spanData;
            }

            List<Span<T>> GatherSpan<T>(List<Span<T>> spans, DateTime startTime, DateTime lastTime) where T : IRecordData
            {
                var result = new List<Span<T>>();
                var duration = lastTime - startTime;

                foreach (var s in spans)
                {
                    var spanDuration = s.leave.Timestamp - s.enter.Timestamp;
                    //var totalDuration = spanDuration + duration;

                    var width = Math.Max(lastTime.Ticks, s.leave.Timestamp.Ticks) - Math.Min(startTime.Ticks, s.enter.Timestamp.Ticks);
                    var allowed = (spanDuration + duration).Ticks;

                    if (width < allowed)
                    {
                        result.Add(s);

                        StartTime = s.enter.Timestamp;
                        LastTime = s.leave.Timestamp;
                        Name = s.enter.Name;
                    }
                }

                return result;
            }
        };
    }
}
