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

namespace ETW
{
    public class Renderable : ContentControl
    {
        public event Action<DrawingContext> Rendering;

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            Rendering?.Invoke(drawingContext);
        }
    }
}
