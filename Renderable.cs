using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Automation.Peers;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Input;

namespace ETW
{
    public class Renderable : Canvas
    {
        class VisualHost : UIElement
        {
            public Visual Visual { get; set; }

            protected override int VisualChildrenCount
            {
                get { return Visual != null ? 1 : 0; }
            }

            protected override Visual GetVisualChild(int index)
            {
                return Visual;
            }
        }

        VisualHost visualHost = new VisualHost();

        public Renderable()
        {
            Children.Add(visualHost);
        }

        public Visual MainVisual
        {
            get
            {
                return visualHost.Visual;
            }
            set
            {
                visualHost.Visual = value;
                Children.Remove(visualHost);
                Children.Add(visualHost);

                // ↓これをしないとイベントが取れない
                AddVisualChild(value);
                AddLogicalChild(value);
            }
        }
    }
}
