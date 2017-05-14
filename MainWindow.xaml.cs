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
            sampler.Start("MECH");
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
