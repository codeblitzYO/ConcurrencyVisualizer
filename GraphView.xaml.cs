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

            ContextSwitch[] timeline = new ContextSwitch[Environment.ProcessorCount];

            var last = DateTime.Now - TimeSpan.FromSeconds(5);
            var start = last - TimeSpan.FromMilliseconds(100);
            var cs = dataSource.GetContextSwitchSpan(start, last);

            foreach (var i in cs)
            {
                ref var old = ref timeline[i.processor];

                if (old.action == ContextSwitch.ActionType.Enter && i.action == ContextSwitch.ActionType.Leave)
                {
                    DrawCpuUsingSpan(drawingContext, brush, i.processor, start, old.timestamp, i.timestamp);
                }
                old = i;
            }
        }

        private void DrawCpuUsingSpan(DrawingContext drawingContext, Brush brush, int cpuNo, DateTime startTime, DateTime usingTime0, DateTime usingTime1)
        {
            var w = (usingTime1 - usingTime0).Ticks / 2000;
            var x = (usingTime1 - startTime).Ticks / 2000;
            var y = cpuNo * 16;
            var h = 14;

            drawingContext.DrawRectangle(brush, null, new Rect(x, y, w, h));
        }
    }
}
