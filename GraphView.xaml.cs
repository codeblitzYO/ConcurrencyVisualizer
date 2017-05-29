using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;
using System.Diagnostics;
using System.Collections;

namespace ETW
{
    /// <summary>
    /// GraphView.xaml の相互作用ロジック
    /// </summary>
    public partial class GraphView : UserControl
    {
        const int RowHeight = 16;

        private Sampler dataSource;
        public Sampler DataSource
        {
            get { return dataSource; }
            set
            {
                dataSource = value;
                renderingTimer.Enabled = value != null;
            }
        }

        struct Span<T>
        {
            public T enter;
            public T leave;
        }

        class Snapshot
        {
            public class Thread
            {
                public ProcessThread processThread;
                public List<List<Span<Marker>>> markerSpan;
                public List<Span<ContextSwitch>> threadSpan;
                public int line;
            }
            public class Processor
            {
                public int index;
                public List<Span<ContextSwitch>> span;
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
                    processors[i] = new Processor()
                    {
                        index = i,
                        span = MakeProcessorSpan(i, contextSwitchSpan)
                    };
                    
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

                    threads[i] = new Thread()
                    {
                        processThread = processThread,
                        markerSpan = markerSpan,
                        threadSpan = MakeThreadSpan(processThread.Id, contextSwitchSpan),
                        line = line
                    };
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

        class SpanGather
        {
            public Snapshot.Thread Thread { get; private set; }
            public List<Span<Marker>> Markers { get; private set; }
            public List<Span<ContextSwitch>> ThreadUsing { get; private set; }

            public DateTime StartTime { get; private set; }
            public DateTime LastTime { get; private set; }
            public TimeSpan Duration { get { return LastTime - StartTime; } }

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
                    if (i.line > line) break;
                    thread = i;
                }

                var spanData = new SpanGather();
                spanData.Thread = thread;
                spanData.Markers = new List<Span<Marker>>();
                spanData.ThreadUsing = new List<Span<ContextSwitch>>();

                var providerIndex = line - thread.line - 1;

                if (providerIndex >= 0 && providerIndex < thread.markerSpan.Count)
                {
                    spanData.Markers = spanData.GatherSpan(thread.markerSpan[providerIndex], startTime, lastTime);
                }
                else
                {
                    spanData.ThreadUsing = spanData.GatherSpan(thread.threadSpan, startTime, lastTime);
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
                    }
                }

                return result;
            }
        };

        private Timer renderingTimer;
        private int processorCount;
        private Typeface defaultTypeface = new Typeface(System.Drawing.SystemFonts.CaptionFont.Name);
        private Snapshot snapshot = new Snapshot();

        private int ProcessorLineStart { get { return 3; } }
        private int ThreadLineStart { get { return ProcessorLineStart + processorCount + 1; } }

        private double timeScale = 200;
        public double TimeScale
        {
            get { return timeScale; }
            set
            {
                timeScale = Math.Max(Math.Min(value, 10000), 10);
                RefreshView();
            }
        }


        public GraphView()
        {
            InitializeComponent();

            Index.Rendering += Index_Rendering;
            Graph.Rendering += Graph_Rendering;
            TimeMeasure.Rendering += Measure_Rendering;

            renderingTimer = new Timer(16.0f);
            //renderingTimer.Elapsed += RenderingTimer_Elapsed;

            var transformGroup = new TransformGroup();
            Graph.RenderTransform = transformGroup;
            transformGroup.Children.Add(new TranslateTransform());
            transformGroup.Children.Add(new ScaleTransform());

            Index.RenderTransform = new TranslateTransform();

            TimeMeasure.RenderTransform = new TranslateTransform();

            processorCount = Environment.ProcessorCount;
        }

        private void Measure_Rendering(DrawingContext drawingContext)
        {
            DrawMeasure(drawingContext, Brushes.Black);
        }

        private void Graph_Rendering(DrawingContext drawingContext)
        {
            DrawProcessorUsage(drawingContext, Brushes.Blue);
            DrawThreadUsage(drawingContext, Brushes.Red);
        }

        private void Index_Rendering(DrawingContext drawingContext)
        {
            // core
            for (var i = 0; i < processorCount; ++i)
            {
                var format = new FormattedText(
                    string.Format("Core{0}", i), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, defaultTypeface, 12.0, Brushes.Black);
                drawingContext.DrawText(format, new Point(8, (ProcessorLineStart + i) * RowHeight));
            }

            // threads
            var line = 0;
            var threads = snapshot.threads;
            for (var i = 0; i < threads.Length; ++i)
            {
                var thread = threads[i];
                var format = new FormattedText(
                    string.Format("Thread {0}", thread.processThread.Id), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, defaultTypeface, 12.0, Brushes.Black);
                drawingContext.DrawText(format, new Point(8, (ThreadLineStart + thread.line) * RowHeight));
                line = Math.Max(line, thread.line);
            }
            ++line;

            ViewScroll.Maximum = Math.Max((ThreadLineStart + line) * RowHeight - ActualHeight, 0);
            ViewScroll.ViewportSize = ActualHeight;
        }

        private void RenderingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Render();
        }

        public void Render()
        {
            var lastTime = DateTime.Now - TimeSpan.FromSeconds(5);
            var startTime = lastTime - TimeSpan.FromSeconds(1);

            snapshot.Refresh(dataSource, startTime, lastTime);

            RefreshView();
        }

        void RefreshView()
        {
            UpdateGraphScrollParam();

            Index.Dispatcher.Invoke(() =>
            {
                Index.InvalidateVisual();
            });
            Graph.Dispatcher.Invoke(() =>
            {
                Graph.InvalidateVisual();
            });
            TimeMeasure.Dispatcher.Invoke(() =>
            {
                TimeMeasure.InvalidateVisual();
            });
        }

        private void UpdateGraphScrollParam()
        {
            GraphScroll.Maximum = TickToPixel((snapshot.lastTime - snapshot.startTime).Ticks) - Graph.ActualWidth;
            GraphScroll.ViewportSize = Graph.ActualWidth;
        }

        private void DrawMeasure(DrawingContext drawingContext, Brush brush)
        {
            var span = snapshot.lastTime - snapshot.startTime;
            var ticks = span.Ticks;

            var pen = new Pen(brush, 1);

            long step = TimeSpan.TicksPerMillisecond;
            long progress = 0;
            while(progress <= ticks)
            {
                var x = TickToPixel(progress);
                drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, 8));

                progress += step;
            }
            //
        }

        private void DrawProcessorUsage(DrawingContext drawingContext, Brush brush)
        {
            foreach (var processor in snapshot.processors)
            {
                foreach(var span in processor.span)
                {
                    DrawSpan(drawingContext, brush, ProcessorLineStart + processor.index, snapshot.startTime, span.enter.Timestamp, span.leave.Timestamp);
                }
            }
        }

        private void DrawThreadUsage(DrawingContext drawingContext, Brush brush)
        {
            foreach (var thread in snapshot.threads)
            {
                foreach (var span in thread.threadSpan)
                {
                    DrawSpan(drawingContext, brush, ThreadLineStart + thread.line, snapshot.startTime, span.enter.Timestamp, span.leave.Timestamp);
                }

                int lineOffset = ThreadLineStart + thread.line;
                foreach (var markerSpan in thread.markerSpan)
                {
                    ++lineOffset;
                    foreach (var span in markerSpan)
                    {
                        DrawSpan(drawingContext, brush, lineOffset, snapshot.startTime, span.enter.Timestamp, span.leave.Timestamp, span.enter.name);
                    }
                }
            }
        }

        private void DrawSpan(DrawingContext drawingContext, Brush brush, int line, DateTime startTime, DateTime usingTime0, DateTime usingTime1, string text = null)
        {
            var w = TickToPixel(Math.Max((usingTime1 - usingTime0).Ticks, 1));
            var x = TickToPixel((usingTime0 - startTime).Ticks);
            var y = line * RowHeight;
            var h = RowHeight - 2;

            drawingContext.DrawRectangle(brush, null, new Rect(x, y, w, h));
            if (!string.IsNullOrEmpty(text) && w > 16)
            {
                var format = new FormattedText(
                    text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, defaultTypeface, 12, Brushes.Black);
                format.MaxTextWidth = w;
                drawingContext.DrawText(format, new Point(x, y));
            }
        }

        private void ViewScroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ((TranslateTransform)Index.RenderTransform).Y = -ViewScroll.Value;

            var group = (TransformGroup)Graph.RenderTransform;
            ((TranslateTransform)group.Children[0]).Y = -ViewScroll.Value;
        }

        private void GraphContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateGraphScrollParam();
        }

        private void GraphScroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var group = (TransformGroup)Graph.RenderTransform;
            ((TranslateTransform)group.Children[0]).X = -e.NewValue;

            ((TranslateTransform)TimeMeasure.RenderTransform).X = -e.NewValue;
        }

        private void Graph_MouseEnter(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(Graph);

            var line = (int)Math.Floor((position.Y - ThreadLineStart * RowHeight) / RowHeight);
            if (line < 0)
            {
                return;
            }

            var startTime = snapshot.startTime + TimeSpan.FromTicks(PixelToTick(position.X));
            var lastTime = startTime + TimeSpan.FromTicks(1);

            var gather = SpanGather.Gather(snapshot, line, startTime, lastTime);
            if (gather != null)
            {
                string content = "";
                content += "ID: " + gather.Thread.processThread.Id + "\n";
                content += "Duration: " + gather.Duration.TotalMilliseconds + "ms\n";
                content += "Start Time: " + gather.StartTime + "\n";
                content += "Last Time: " + gather.LastTime + "\n";

                var formattedText = new FormattedText(
                    content, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, defaultTypeface, Description.FontSize, Brushes.Black);

                Description.Width = formattedText.Width;
                Description.Height = formattedText.Height;
                Description.Content = content;
            }

            Description.Visibility = Visibility.Visible;
            Description.Margin = new Thickness(position.X - GraphScroll.Value, position.Y - ViewScroll.Value, 0, 0);
        }

        private void Graph_MouseLeave(object sender, MouseEventArgs e)
        {
            Description.Visibility = Visibility.Hidden;
        }

        private double TickToPixel(long ticks)
        {
            return ticks / timeScale;
        }

        private long PixelToTick(double pixels)
        {
            return (long)(pixels * timeScale);
        }
    }
}
