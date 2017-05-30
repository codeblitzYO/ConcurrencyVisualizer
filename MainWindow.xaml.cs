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
    class ApplicationData
    {
        public float TimeScale { set; get; }
    }

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private Sampler sampler;


        public MainWindow()
        {
            InitializeComponent();

			Graph.ChangedTimeScale += Graph_ChangedTimeScale;
            Scale.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(Scale_MouseDown), true);
            Scale.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(Scale_MouseUp), true);
        }

		private void Graph_ChangedTimeScale(object sender, double e)
		{
			Scale.Value = e;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sampler != null)
            {
                sampler.Stop();
            }
            Graph.DataSource = null;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            StopSampling();

            var newSampler = new Sampler();
            if (newSampler.Start(ProcessName.Text))
            {
                Graph.DataSource = newSampler;
                sampler = newSampler;
            }
            else
            {
                Start.ToolTip = new ToolTip() { Content = "Process not found.", IsOpen = true };
            }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            StopSampling();
        }

        private void StopSampling()
        {
            if (sampler != null)
            {
                sampler.Stop();
                sampler = null;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if(sampler != null)
            {
                Graph.Render();
            }
        }

        private void Scale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Graph.TimeScale = ((float)e.NewValue);
        }

		private void Scale_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Graph.SimpleRendering = true;
		}

		private void Scale_MouseUp(object sender, MouseButtonEventArgs e)
		{
			Graph.SimpleRendering = false;
			Graph.TimeScale = Graph.TimeScale;
		}

        private void Start_MouseLeave(object sender, MouseEventArgs e)
        {
            if (Start.ToolTip != null)
            {
                ((ToolTip)Start.ToolTip).IsOpen = false;
            }
            Start.ToolTip = null;
        }
    }
}
