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
            public int line;
            public ContextSwitch contextSwitch;
        }

        private Timer renderingTimer;
        private int processorCount;

        private int ProcessorLineStart { get { return 0; } }
        private int ThreadLineStart { get { return ProcessorLineStart + processorCount + 1; } }

        public GraphView()
        {
            InitializeComponent();

            Index.Rendering += Index_Rendering;
            Graph.Rendering += Graph_Rendering;

            renderingTimer = new Timer(10.0f);
            renderingTimer.Elapsed += RenderingTimer_Elapsed;

            processorCount = Environment.ProcessorCount;
        }

        private void Graph_Rendering(DrawingContext drawingContext)
        {
            if (dataSource == null)
            {
                return;
            }

            var lastTime = DateTime.Now - TimeSpan.FromSeconds(5);
            var startTime = lastTime - TimeSpan.FromMilliseconds(100);

            var contextSwitchData = dataSource.GetContextSwitchSpan(startTime, lastTime);

            drawProcessorUsage(drawingContext, Brushes.Blue, contextSwitchData, startTime, lastTime);
            drawThreadUsage(drawingContext, Brushes.Red, contextSwitchData, startTime, lastTime);
        }

        private void Index_Rendering(DrawingContext drawingContext)
        {
            if (dataSource == null)
            {
                return;
            }

            var typeface = new Typeface(System.Drawing.SystemFonts.CaptionFont.Name);

            // core
            for (var i = 0; i < processorCount; ++i)
            {
                var format = new FormattedText(
                    string.Format("Core{0}", i), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12.0, Brushes.Black);
                drawingContext.DrawText(format, new Point(8, (ProcessorLineStart + i) *RowHeight));
            }

            // threads
            var threads = dataSource.TargetProcess.Threads;
            for (var i = 0; i < threads.Count; ++i)
            {
                var thread = threads[i];
                var format = new FormattedText(
                    string.Format("Thread {0}", thread.Id), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12.0, Brushes.Black);
                drawingContext.DrawText(format, new Point(8, (ThreadLineStart + i) * RowHeight));
            }

            ViewScroll.Maximum = Math.Max((ThreadLineStart + threads.Count) * RowHeight - ActualHeight, 0);
            ViewScroll.ViewportSize = ActualHeight;
        }

        private void RenderingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Index.Dispatcher.Invoke(() =>
            {
                Index.InvalidateVisual();
            });
            Graph.Dispatcher.Invoke(() =>
            {
                Graph.InvalidateVisual();
            });
        }

        private void drawProcessorUsage(DrawingContext drawingContext, Brush brush, List<ContextSwitch> cs, DateTime startTime, DateTime lastTime)
        {
            ContextSwitch[] timeline = new ContextSwitch[Environment.ProcessorCount];

            foreach (var i in cs)
            {
                ref var old = ref timeline[i.processor];

                if (old.action == ContextSwitch.ActionType.Enter && i.action == ContextSwitch.ActionType.Leave)
                {
                    DrawSpan(drawingContext, brush, i.processor, startTime, old.timestamp, i.timestamp);
                }
                if (i.action != ContextSwitch.ActionType.Stay)
                {
                    old = i;
                }
            }
        }

        private void drawThreadUsage(DrawingContext drawingContext, Brush brush, List<ContextSwitch> cs, DateTime startTime, DateTime lastTime)
        {
            Dictionary<int, ThreadUsage> timeline = new Dictionary<int, ThreadUsage>();

            int line = 0;
            foreach (ProcessThread i in dataSource.TargetProcess.Threads)
            {
                var usage = new ThreadUsage() { line = line };
                for (int k = 0; k < MarkerRecorder.ProvidersGuid.Length; ++k)
                {
                    var markers = dataSource.GetMarkerSpan(i.Id, k, startTime, lastTime);
                    if (markers != null && markers.Count > 0)
                    {
                        line++;
                        drawThreadMarker(drawingContext, markers, ThreadLineStart + line, startTime, lastTime);
                    }
                }
                timeline[i.Id] = usage;
                line++;
            }

            foreach (var i in cs)
            {
                ThreadUsage thread;
                if (!timeline.TryGetValue(i.oldThread, out thread))
                {
                    continue;
                }

                if (thread.contextSwitch.Timestamp.Ticks > 0)
                {
                    DrawSpan(drawingContext, brush, ThreadLineStart + thread.line, startTime, thread.contextSwitch.timestamp, i.timestamp);
                    thread.contextSwitch.timestamp = new DateTime();
                }

                if (timeline.ContainsKey(i.newThread))
                {
                    ref var data = ref timeline[i.newThread].contextSwitch;
                    if (data.timestamp.Ticks == 0)
                    {
                        data = i;
                    }
                }
            }
        }

        private void drawThreadMarker(DrawingContext drawingContext, List<Marker> markers, int line, DateTime startTime, DateTime lastTime)
        {
            Marker p = new Marker();
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
                            DrawSpan(drawingContext, Brushes.Azure, line, startTime, p.Timestamp, i.Timestamp, p.name);
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
            var w = (usingTime1 - usingTime0).Ticks / 1000;
            var x = (usingTime1 - startTime).Ticks / 1000;
            var y = line * RowHeight;
            var h = RowHeight - 2;

            drawingContext.DrawRectangle(brush, null, new Rect(x, y, w, h));
            if(!string.IsNullOrEmpty(text))
            {
                var typeface = new Typeface(System.Drawing.SystemFonts.CaptionFont.Name);
                var format = new FormattedText(
                    text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12, Brushes.Black);
                drawingContext.DrawText(format, new Point(x, y));
            }
        }

        private void ViewScroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Index.RenderTransform = new TranslateTransform(0, -ViewScroll.Value);
            Graph.RenderTransform = new TranslateTransform(-100, -ViewScroll.Value);
        }
    }
}
