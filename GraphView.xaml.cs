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
            public struct Thread
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

        private Timer renderingTimer;
        private int processorCount;
        private Typeface defaultTypeface = new Typeface(System.Drawing.SystemFonts.CaptionFont.Name);
        private Snapshot snapshot = new Snapshot();

        private int ProcessorLineStart { get { return 3; } }
        private int ThreadLineStart { get { return ProcessorLineStart + processorCount + 1; } }

        private float timeScale = 200;
        public float TimeScale
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

            renderingTimer = new Timer(16.0f);
            //renderingTimer.Elapsed += RenderingTimer_Elapsed;

            var transformGroup = new TransformGroup();
            Graph.RenderTransform = transformGroup;
            transformGroup.Children.Add(new TranslateTransform());
            transformGroup.Children.Add(new ScaleTransform());

            Index.RenderTransform = new TranslateTransform();

            processorCount = Environment.ProcessorCount;
        }

        private void Graph_Rendering(DrawingContext drawingContext)
        {
            DrawMeasure(drawingContext, Brushes.Black);
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
            GraphScroll.Maximum = (snapshot.lastTime - snapshot.startTime).Ticks / TimeScale;

            Index.Dispatcher.Invoke(() =>
            {
                Index.InvalidateVisual();
            });
            Graph.Dispatcher.Invoke(() =>
            {
                Graph.InvalidateVisual();
            });
        }

        private void DrawMeasure(DrawingContext drawingContext, Brush brush)
        {

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
            var w = Math.Max((usingTime1 - usingTime0).Ticks / timeScale, 1);
            var x = (usingTime1 - startTime).Ticks / timeScale;
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
            GraphScroll.ViewportSize = e.NewSize.Width;
        }

        private void GraphScroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var group = (TransformGroup)Graph.RenderTransform;
            ((TranslateTransform)group.Children[0]).X = -e.NewValue;


        }
    }
}
