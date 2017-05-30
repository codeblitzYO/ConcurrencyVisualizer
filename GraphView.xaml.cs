using System;
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
using System.Windows.Threading;
using System.Timers;
using System.Collections;
using System.Threading;

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
			}
		}

		private int processorCount;
		private Typeface defaultTypeface = new Typeface(System.Drawing.SystemFonts.CaptionFont.Name);
		private Snapshot snapshot = new Snapshot();
		private Pen spanFramePen = new Pen(Brushes.LightGray, 0.5f);
		private bool measureDragging = false;

		private int ProcessorLineStart { get { return 3; } }
		private int ThreadLineStart { get { return ProcessorLineStart + processorCount + 1; } }

		public event Action<object, double> ChangedTimeScale;

		private double timeScale = 200;
		public double TimeScale
		{
			get { return timeScale; }
			set
			{
				timeScale = Math.Max(Math.Min(value, 10000), 1);
				RefreshView();
			}
		}

		public bool SimpleRendering
		{
			get; set;
		}

		public GraphView()
		{
			InitializeComponent();

			var transformGroup = new TransformGroup();
			Graph.RenderTransform = transformGroup;
			transformGroup.Children.Add(new TranslateTransform());
			transformGroup.Children.Add(new ScaleTransform());
			Index.RenderTransform = new TranslateTransform();
			TimeMeasure.RenderTransform = new TranslateTransform();

			processorCount = Environment.ProcessorCount;
		}

		private void Measure_Rendering()
		{
			var visual = new DrawingVisual();

			using (var context = visual.RenderOpen())
			{
				DrawMeasure(context, Brushes.Black);
			}

			TimeMeasure.MainVisual = visual;
		}

		private void Graph_Rendering()
		{
			var visual = new DrawingVisual();
			using (var context = visual.RenderOpen())
			{
				DrawProcessorUsage(context, Brushes.Blue);
				DrawThreadUsage(context, Brushes.Red);
			}
			Graph.MainVisual = visual;
		}

		private void Index_Rendering()
		{
			var visual = new DrawingVisual();

			using (var context = visual.RenderOpen())
			{

				// core
				for (var i = 0; i < processorCount; ++i)
				{
					var format = new FormattedText(
						string.Format("Core{0}", i), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, defaultTypeface, 12.0, Brushes.Black);
					context.DrawText(format, new Point(8, (ProcessorLineStart + i) * RowHeight));
				}

				// threads
				var line = 0;
				var threads = snapshot.threads;
				for (var i = 0; i < threads.Length; ++i)
				{
					var thread = threads[i];
					var format = new FormattedText(
						string.Format("Thread {0}", thread.ProcessThread.Id), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, defaultTypeface, 12.0, Brushes.Black);
					context.DrawText(format, new Point(8, (ThreadLineStart + thread.Line) * RowHeight));
					line = Math.Max(line, thread.Line);
				}
				++line;

				ViewScroll.Maximum = Math.Max((ThreadLineStart + line) * RowHeight - ActualHeight, 0);
				ViewScroll.ViewportSize = ActualHeight;
			}

			Index.MainVisual = visual;
		}

		private void RenderingTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			Render();
		}

		public void Render()
		{
			var lastTime = DateTime.Now - TimeSpan.FromSeconds(2);
			var startTime = lastTime - TimeSpan.FromSeconds(0.2f);

			snapshot.Refresh(dataSource, startTime, lastTime);

			RefreshView();
		}

		void RefreshView()
		{
			UpdateGraphScrollParam();

			Index_Rendering();
			Graph_Rendering();
			Measure_Rendering();
		}

		private void UpdateGraphScrollParam()
		{
			GraphScroll.Maximum = TickToPixel((snapshot.lastTime - snapshot.startTime).Ticks) - Graph.ActualWidth;
			GraphScroll.ViewportSize = Graph.ActualWidth;
		}

		private void DrawMeasure(DrawingContext drawingContext, Brush brush)
		{
			var span = snapshot.lastTime - snapshot.startTime;
			var ticks = span.Ticks;

			drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, TickToPixel(span.Ticks), TimeMeasure.Height));

			var pen = new Pen(brush, 1);

			long step = TimeSpan.TicksPerMillisecond;
			long progress = 0;
			while (progress <= ticks)
			{
				var x = TickToPixel(progress);
				drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, 8));

				progress += step;
			}
			//
		}

		private void DrawProcessorUsage(DrawingContext drawingContext, Brush brush)
		{
			foreach (var processor in snapshot.processors)
			{
				foreach (var span in processor.Spans)
				{
					DrawSpan(drawingContext, brush, ProcessorLineStart + processor.Index, snapshot.startTime, span.enter.Timestamp, span.leave.Timestamp);
				}
			}
		}

		private void DrawThreadUsage(DrawingContext drawingContext, Brush brush)
		{
			foreach (var thread in snapshot.threads)
			{
				foreach (var span in thread.ThreadSpan)
				{
					DrawSpan(drawingContext, brush, ThreadLineStart + thread.Line, snapshot.startTime, span.enter.Timestamp, span.leave.Timestamp);
				}

				int lineOffset = ThreadLineStart + thread.Line;
				foreach (var markerSpan in thread.MarkerSpan)
				{
					++lineOffset;
					foreach (var span in markerSpan)
					{
						DrawSpan(drawingContext, Brushes.Azure, lineOffset, snapshot.startTime, span.enter.Timestamp, span.leave.Timestamp, span.enter.Name);
					}
				}
			}
		}

		private void DrawSpan(DrawingContext drawingContext, Brush brush, int line, DateTime startTime, DateTime usingTime0, DateTime usingTime1, string text = null)
		{
			var w = TickToPixel(Math.Max((usingTime1 - usingTime0).Ticks, 1));
			var x = TickToPixel((usingTime0 - startTime).Ticks);
			var y = line * RowHeight;
			var h = RowHeight - 2;

			if (SimpleRendering)
			{
				if (w > 8)
				{
					drawingContext.DrawRectangle(brush, null, new Rect(x, y, w, h));
				}
			}
			else
			{
				var pen = w > 16 ? spanFramePen : null;
				drawingContext.DrawRectangle(brush, pen, new Rect(x, y, w, h));
				if (!string.IsNullOrEmpty(text) && w > 16)
				{
					var format = new FormattedText(
						text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, defaultTypeface, 12, Brushes.Black);
					format.MaxTextWidth = w;
					drawingContext.DrawText(format, new Point(x + 2, y));
				}
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
			UpdateGraphScrollParam();
		}

		private void GraphScroll_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			var group = (TransformGroup)Graph.RenderTransform;
			((TranslateTransform)group.Children[0]).X = -e.NewValue;

			((TranslateTransform)TimeMeasure.RenderTransform).X = -e.NewValue;
		}

		private void Graph_MouseEnter(object sender, MouseEventArgs e)
		{

		}

		private void Graph_MouseLeave(object sender, MouseEventArgs e)
		{
			Description.Visibility = Visibility.Hidden;
		}

		private void Graph_MouseMove(object sender, MouseEventArgs e)
		{
			var position = e.GetPosition(Graph);

			var line = (int)Math.Floor((position.Y - ThreadLineStart * RowHeight) / RowHeight);
			if (line < 0)
			{
				return;
			}

			var startTime = snapshot.startTime + TimeSpan.FromTicks(PixelToTick(position.X));
			var lastTime = startTime + TimeSpan.FromTicks(1);

			var gather = SpanGather.Gather(snapshot, line, startTime, lastTime);
			if (gather != null)
			{
				string content = "";
				content += "ID: " + gather.Thread.ProcessThread.Id + "\n";
				if (gather.Name != null) content += "Name: " + gather.Name + "\n";
				content += "Duration: " + gather.Duration.TotalMilliseconds + "ms\n";
				content += "Start Time: " + gather.StartTime + "\n";
				content += "Last Time: " + gather.LastTime + "\n";

				Description.Content = content;
			}

			Description.Visibility = Visibility.Visible;
			Description.Margin = new Thickness(position.X - GraphScroll.Value, position.Y - ViewScroll.Value, 0, 0);
		}

		private double TickToPixel(long ticks)
		{
			return ticks / timeScale;
		}

		private long PixelToTick(double pixels)
		{
			return (long)(pixels * timeScale);
		}

		private void TimeMeasure_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (measureDragging) return;

			var position = e.GetPosition(TimeMeasure);
			Canvas.SetLeft(TimeMeasureSelect, position.X);
			TimeMeasureSelect.Width = 0;

			measureDragging = true;
		}

		private void TimeMeasure_MouseUp(object sender, MouseButtonEventArgs e)
		{
			if (!measureDragging) return;

			var left = Canvas.GetLeft(TimeMeasureSelect);
			var startTick = PixelToTick(left);
			var startTime = snapshot.startTime + TimeSpan.FromTicks(startTick);
			var lastTime = startTime + TimeSpan.FromTicks(PixelToTick(TimeMeasureSelect.Width));

			var duration = lastTime - startTime;

			ChangedTimeScale?.Invoke(this, duration.Ticks / Graph.ActualWidth);
			GraphScroll.Value = TickToPixel(startTick);			

			TimeMeasureSelect.Width = 0;
			measureDragging = false;
		}

		private void TimeMeasure_MouseMove(object sender, MouseEventArgs e)
		{
			if (!measureDragging) return;

			var position = e.GetPosition(TimeMeasure);

			var left = Canvas.GetLeft(TimeMeasureSelect);
			TimeMeasureSelect.Width = Math.Max(position.X - left, 0);
		}
	}
}
