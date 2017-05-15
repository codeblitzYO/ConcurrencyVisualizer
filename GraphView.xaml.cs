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
            drawingContext.DrawRectangle(brush, null, new Rect(0, 0, 100, 100));

            if (dataSource == null)
            {
                return;
            }

            var now = DateTime.Now - TimeSpan.FromMilliseconds(1000);
            var leave = now - TimeSpan.FromMilliseconds(100);
            var cs = dataSource.GetContextSwitchSpan(leave, now);

        }
    }
}
