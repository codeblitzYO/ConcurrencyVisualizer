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

        class ThreadUsage
        {
            public Snapshot.Thread thread;
            public ContextSwitch contextSwitch;
        }


        class Snapshot
        {
            public class Thread
            {
                public ProcessThread processThread;
                public List<List<Marker>> markerSpan;
                public int line;
            }

            public Thread[] threads = new Thread[0];
            public List<ContextSwitch> contextSwitchSpan = new List<ContextSwitch>();
            public DateTime startTime;
            public DateTime lastTime;

            public void Refresh(Sampler dataSource, DateTime startTime, DateTime lastTime)
            {
                this.startTime = startTime;
                this.lastTime = lastTime;

                int line = 0;
                threads = new Thread[dataSource.TargetProcess.Threads.Count];
                for (var i = 0; i < dataSource.TargetProcess.Threads.Count; ++i)
                {
                    var processThread = dataSource.TargetProcess.Threads[i];

                    var markerSpan = new List<List<Marker>>();
                    for (int k = 0; k < MarkerRecorder.ProvidersGuid.Length; ++k)
                    {
                        var markers = dataSource.GetMarkerSpan(processThread.Id, k, startTime, lastTime);
                        if (markers != null && markers.Count > 0)
                        {
                            markerSpan.Add(markers);
                        }
                    }

                    threads[i] = new Thread()
                    {
                        processThread = processThread,
                        markerSpan = markerSpan,
                        line = line
                    };
                    line += markerSpan.Count + 1;
                }

                contextSwitchSpan = dataSource.GetContextSwitchSpan(startTime, lastTime);
            }
        }

        class SpanData
        {
            public Snapshot.Thread Thread { get; private set; }
            public List<Marker> Markers { get; private set; }
            public List<ContextSwitch> ConstextSwitches { get; private set; }

            public DateTime StartTime { get; private set; }
            public DateTime LastTime { get; private set; }
            public TimeSpan Duration { get; private set; }

            SpanData()
            {
            }

            static public SpanData Gather(Snapshot snapshot, int line, DateTime startTime, DateTime lastTime)
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

                var spanData = new SpanData();
                spanData.Thread = thread;
                spanData.Markers = new List<Marker>();
                spanData.ConstextSwitches = new List<ContextSwitch>();

                var providerIndex = line - thread.line - 1;

                if (providerIndex >= 0 && providerIndex < thread.markerSpan.Count)
                {
                    spanData.GatherMarker(thread.markerSpan[providerIndex], startTime, lastTime);
                }
                else
                {
                    spanData.GatherContextSwitch(snapshot, startTime, lastTime);
                }

                return spanData;
            }

            void GatherMarker(List<Marker> markerArray, DateTime startTime, DateTime lastTime)
            {
                var timeSpan = lastTime - startTime;

                Action<Marker, Marker> saveStat = (x, y) =>
                {
                    StartTime = x.timestamp;
                    LastTime = y.timestamp;
                    Duration = LastTime - StartTime;
                };

                for (var i = 0; i < markerArray.Count; ++i)
                {
                    var marker = markerArray[i];
                    if (marker.timestamp >= lastTime)
                    {
                        break;
                    }
                    switch (marker.e)
                    {
                        case Marker.Event.EnterSpan:
                            for (var j = i + 1; j < markerArray.Count; ++j)
                            {
                                var marker2 = markerArray[j];
                                if (marker2.e == Marker.Event.LeaveSpan && marker.id == marker2.id)
                                {
                                    var span = marker2.timestamp - marker.timestamp;
                                    var width = Math.Max(marker2.timestamp.Ticks, lastTime.Ticks) - Math.Min(marker.timestamp.Ticks, startTime.Ticks);
                                    if (width <= timeSpan.Ticks + span.Ticks)
                                    {
                                        Markers.Add(marker);
                                        Markers.Add(marker2);
                                        saveStat(marker, marker2);
                                        break;
                                    }
                                }
                            }
                            break;
                        case Marker.Event.Flag:
                        case Marker.Event.Message:
                            if (marker.timestamp >= startTime && marker.timestamp < lastTime)
                            {
                                Markers.Add(marker);
                                saveStat(marker, marker);
                            }
                            break;
                    }
                }
                Markers.Sort((x, y) =>
                {
                    return x.timestamp.CompareTo(y.timestamp);
                });
            }

            void GatherContextSwitch(Snapshot snapshot, DateTime startTime, DateTime lastTime)
            {
                Action<ContextSwitch, ContextSwitch> saveStat = (x, y) =>
                {
                    StartTime = x.timestamp;
                    LastTime = y.timestamp;
                    Duration = LastTime - StartTime;
                };

                var timeSpan = lastTime - startTime;
                var contextSwiches = snapshot.contextSwitchSpan;
                for (var i = 0; i < contextSwiches.Count; ++i)
                {
                    var contextSwitch = contextSwiches[i];
                    if (contextSwitch.timestamp >= lastTime)
                    {
                        break;
                    }
                    if (contextSwitch.newThread == Thread.processThread.Id)
                    {
                        for (var j = i + 1; j < contextSwiches.Count; ++j)
                        {
                            var contextSwitch2 = contextSwiches[j];
                            if (contextSwitch2.oldThread != Thread.processThread.Id)
                            {
                                continue;
                            }

                            var span = contextSwitch2.timestamp - contextSwitch.timestamp;
                            var width = Math.Max(contextSwitch2.timestamp.Ticks, lastTime.Ticks) - Math.Min(contextSwitch.timestamp.Ticks, startTime.Ticks);

                            if (width <= timeSpan.Ticks + span.Ticks)
                            {
                                ConstextSwitches.Add(contextSwitch);
                                ConstextSwitches.Add(contextSwitch2);
                                saveStat(contextSwitch, contextSwitch2);
                                break;
                            }
                        }
                    }
                }
                ConstextSwitches.Sort((x, y) =>
                {
                    return x.timestamp.CompareTo(y.timestamp);
                });
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
            ContextSwitch[] timeline = new ContextSwitch[Environment.ProcessorCount];

            foreach (var i in snapshot.contextSwitchSpan)
            {
                ref var old = ref timeline[i.processor];

                if (old.action == ContextSwitch.ActionType.Enter && i.action == ContextSwitch.ActionType.Leave)
                {
                    DrawSpan(drawingContext, brush, ProcessorLineStart + i.processor, snapshot.startTime, old.timestamp, i.timestamp);
                }
                if (i.action != ContextSwitch.ActionType.Stay)
                {
                    old = i;
                }
            }
        }

        private void DrawThreadUsage(DrawingContext drawingContext, Brush brush)
        {
            var timeline = new Dictionary<int, ThreadUsage>();
            timeline[0] = new ThreadUsage();

            foreach (var thread in snapshot.threads)
            {
                timeline[thread.processThread.Id] = new ThreadUsage() { thread = thread };
                for (var k = 0; k < thread.markerSpan.Count; ++k)
                {
                    DrawThreadMarker(drawingContext, thread.markerSpan[k], ThreadLineStart + thread.line + k + 1);
                }
            }

            foreach (var i in snapshot.contextSwitchSpan)
            {
                try
                {
                    if (i.oldThread != 0)
                    {
                        ThreadUsage thread = timeline[i.oldThread];

                        if (thread.contextSwitch.Timestamp.Ticks > 0)
                        {
                            DrawSpan(drawingContext, brush, ThreadLineStart + thread.thread.line, snapshot.startTime, thread.contextSwitch.timestamp, i.timestamp);
                            thread.contextSwitch.timestamp = new DateTime();
                        }
                    }

                    ref var data = ref timeline[i.newThread].contextSwitch;
                    if (data.timestamp.Ticks == 0)
                    {
                        data = i;
                    }
                }
                catch (KeyNotFoundException)
                {
                }
            }
        }

        private void DrawThreadMarker(DrawingContext drawingContext, List<Marker> markers, int line)
        {
            Marker p = new Marker()
            {
                timestamp = snapshot.startTime
            };
            foreach (var i in markers)
            {
                switch (i.e)
                {
                    case Marker.Event.EnterSpan:
                        p = i;
                        break;
                    case Marker.Event.LeaveSpan:
                        if (p.e == Marker.Event.EnterSpan)
                        {
                            DrawSpan(drawingContext, Brushes.Azure, line, snapshot.startTime, p.Timestamp, i.Timestamp, p.name);
                            p.e = Marker.Event.Unknown;
                        }
                        break;
                    case Marker.Event.Flag:
                        break;
                    case Marker.Event.Message:
                        break;
                    case Marker.Event.Unknown:
                        break;
                }
            }
        }

        private void DrawSpan(DrawingContext drawingContext, Brush brush, int line, DateTime startTime, DateTime usingTime0, DateTime usingTime1, string text = null)
        {
            var w = TickToPixel(Math.Max((usingTime1 - usingTime0).Ticks, 1));
            var x = TickToPixel((usingTime1 - startTime).Ticks);
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

            var gather = SpanData.Gather(snapshot, line, startTime, lastTime);
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
