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
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private Sampler sampler;
        

        public MainWindow()
        {
            InitializeComponent();

            sampler = new Sampler();
            sampler.Start("CV");

            Graph.DataSource = sampler;
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
            
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
