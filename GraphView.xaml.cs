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
        private Random g = new Random();

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
            public int index;
            public ContextSwitch contextSwitch;
        }

        private Timer renderingTimer;

        public GraphView()
        {
            InitializeComponent();

            renderingTimer = new Timer(10.0f);
            renderingTimer.Elapsed += RenderingTimer_Elapsed;
        }

        private void RenderingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                InvalidateVisual();
            });
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var brush = new SolidColorBrush(Color.FromRgb(
                (byte)(g.Next() % 255),
                (byte)(g.Next() % 255),
                (byte)(g.Next() % 255)));

            if (dataSource == null)
            {
                return;
            }

            var lastTime = DateTime.Now - TimeSpan.FromSeconds(5);
            var startTime = lastTime - TimeSpan.FromMilliseconds(50);

            var contextSwitchData = dataSource.GetContextSwitchSpan(startTime, lastTime);

            drawProcessorUsage(drawingContext, Brushes.Blue, contextSwitchData, startTime, lastTime);
            drawThreadUsage(drawingContext, Brushes.Red, contextSwitchData, startTime, lastTime);

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

            int count = 0;
            foreach (ProcessThread i in dataSource.TargetProcess.Threads)
            {
                timeline[i.Id] = new ThreadUsage() { index = count++ };
            }

            int lineStart = 10;

            foreach (var i in cs)
            {
                ThreadUsage thread;
                if (!timeline.TryGetValue(i.oldThread, out thread))
                {
                    continue;
                }

                if (thread.contextSwitch.Timestamp.Ticks > 0)
                {
                    DrawSpan(drawingContext, brush, lineStart + thread.index, startTime, thread.contextSwitch.timestamp, i.timestamp);
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

        private void DrawSpan(DrawingContext drawingContext, Brush brush, int line, DateTime startTime, DateTime usingTime0, DateTime usingTime1)
        {
            var w = (usingTime1 - usingTime0).Ticks / 1000;
            var x = (usingTime1 - startTime).Ticks / 1000;
            var y = line * 16;
            var h = 14;

            drawingContext.DrawRectangle(brush, null, new Rect(x, y, w, h));
        }
    }
}
