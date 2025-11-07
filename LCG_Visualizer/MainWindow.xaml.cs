using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LcgVisualizer
{
    public partial class MainWindow : Window
    {
        private const double CircleRadius = 20; // Circle radius (half of Width/Height)

        public MainWindow()
        {
            InitializeComponent();
            // Set fullscreen mode
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
        }

        /// <summary>
        /// Close button click event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Visualize button click event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void Visualize_Click(object sender, RoutedEventArgs e)
        {
            // Clear canvas
            DrawingCanvas.Children.Clear();

            // Parse input values
            if (!long.TryParse(ModulusTextBox.Text, out long m) || m <= 0)
            {
                MessageBox.Show("Modulus (m) must be a positive integer.");
                return;
            }
            if (!long.TryParse(MultiplierTextBox.Text, out long a))
            {
                MessageBox.Show("Multiplier (a) must be an integer.");
                return;
            }
            if (!long.TryParse(IncrementTextBox.Text, out long c))
            {
                MessageBox.Show("Increment (c) must be an integer.");
                return;
            }
            if (!long.TryParse(SeedTextBox.Text, out long initialSeed))
            {
                MessageBox.Show("Seed must be an integer.");
                return;
            }

            // Normalize seed value to valid range
            initialSeed = ((initialSeed % m) + m) % m;

            // Force layout update to get canvas size
            DrawingCanvas.UpdateLayout();

            // Generate and visualize LCG
            await VisualizeLcgAsync(m, a, c, initialSeed);
        }

        /// <summary>
        /// LCG visualization
        /// </summary>
        private async Task VisualizeLcgAsync(long m, long a, long c, long initialSeed)
        {
            // Verify canvas size
            if (DrawingCanvas.ActualWidth <= 0 || DrawingCanvas.ActualHeight <= 0)
            {
                MessageBox.Show("Cannot determine canvas size.");
                return;
            }

            // Visualization parameters
            double centerX = DrawingCanvas.ActualWidth / 2;
            double centerY = DrawingCanvas.ActualHeight / 2;
            double radius = Math.Min(centerX, centerY) * 0.8; // Circle radius

            // Calculate position for each number
            Point GetPointForNumber(long number)
            {
                double angle = (double)number / m * 2 * Math.PI - Math.PI / 2; // -PI/2 places 0 at 12 o'clock
                return new Point(centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle));
            }

            // Calculate edge point of circle (for arrows to start/end outside the circle)
            Point GetEdgePoint(Point center, Point target)
            {
                double dx = target.X - center.X;
                double dy = target.Y - center.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance == 0) return center;

                // Calculate point at circle radius distance
                double offsetX = (dx / distance) * CircleRadius;
                double offsetY = (dy / distance) * CircleRadius;

                return new Point(center.X + offsetX, center.Y + offsetY);
            }

            // Display numbers in circular layout
            for (int i = 0; i < m; i++)
            {
                Point p = GetPointForNumber(i);
                Ellipse ellipse = new()
                {
                    Width = CircleRadius * 2,
                    Height = CircleRadius * 2,
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromRgb(60, 60, 60))
                };
                Canvas.SetLeft(ellipse, p.X - ellipse.Width / 2);
                Canvas.SetTop(ellipse, p.Y - ellipse.Height / 2);
                DrawingCanvas.Children.Add(ellipse);

                TextBlock text = new()
                {
                    Text = i.ToString(),
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Foreground = Brushes.White
                };
                text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(text, p.X - text.DesiredSize.Width / 2);
                Canvas.SetTop(text, p.Y - text.DesiredSize.Height / 2);
                DrawingCanvas.Children.Add(text);
            }

            // Generate random sequence and draw path
            long currentX = initialSeed;
            int step = 0;

            // Store visited numbers to prevent infinite loops and detect cycles
            Dictionary<long, int> history = [];

            while (!history.ContainsKey(currentX))
            {
                history.Add(currentX, step);

                /* LCG Formula (https://en.wikipedia.org/wiki/Linear_congruential_generator)
                 * ============================================================
                 * X_{n+1} = (a * X_n + c) mod m
                 * Next random = (multiplier * previous random + increment) mod modulus
                 * ===========================================================
                 * Easy parameter selection guide:
                 * seed: initial value 0
                 * m: set to desired number (or range) of random values
                 * a: (a-1) must be divisible by all prime factors of m
                 * c: coprime with m (1 recommended)
                 */
                long nextX = ((a * currentX + c) % m + m) % m;

                // Draw line (arrow) - starting/ending at circle edges
                Point startCenter = GetPointForNumber(currentX);
                Point endCenter = GetPointForNumber(nextX);

                // Adjust arrow to start and end at circle edges
                Point startPoint = GetEdgePoint(startCenter, endCenter);
                Point endPoint = GetEdgePoint(endCenter, startCenter);

                Line line = new()
                {
                    X1 = startPoint.X,
                    Y1 = startPoint.Y,
                    X2 = endPoint.X,
                    Y2 = endPoint.Y,
                    Stroke = GetStepColor(step),
                    StrokeThickness = 4
                };
                DrawingCanvas.Children.Add(line);

                // Draw arrowhead
                DrawArrowhead(DrawingCanvas, startPoint, endPoint, GetStepColor(step));

                // Wait 0.1 seconds (animation effect)
                await Task.Delay(100);

                currentX = nextX;
                step++;

                // Safety check: prevent too many steps
                if (step > m * 2)
                {
                    MessageBox.Show("Unable to find cycle after too many steps. Please check parameters.");
                    break;
                }
            }

            // Mark cycle start point
            if (history.TryGetValue(currentX, out int preCycleLength))
            {
                Point cycleStartPoint = GetPointForNumber(currentX);
                Ellipse cycleStartMarker = new()
                {
                    Width = 50,
                    Height = 50,
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 100, 100)),
                    StrokeThickness = 4,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(cycleStartMarker, cycleStartPoint.X - cycleStartMarker.Width / 2);
                Canvas.SetTop(cycleStartMarker, cycleStartPoint.Y - cycleStartMarker.Height / 2);
                DrawingCanvas.Children.Add(cycleStartMarker);

                // Display cycle information
                int cycleLength = step - preCycleLength;
                MessageBox.Show($"Random sequence cycle found!\nCycle start value: {currentX}\nCycle length: {cycleLength}\nPre-cycle length: {preCycleLength}");
            }
        }

        /// <summary>
        /// Get color based on step
        /// </summary>
        /// <param name="step"></param>
        /// <returns></returns>
        private Brush GetStepColor(int step)
        {
            Brush[] colors = new Brush[9];

            for (int i = 0; i < 9; i++)
            {
                colors[i] = GetStepColor(i + 1, 9);
            }

            return colors[step % colors.Length];
        }

        /// <summary>
        /// Calculate color for each step
        /// </summary>
        private SolidColorBrush GetStepColor(int step, int totalSteps)
        {
            // 1. Define base color
            Color baseColor = (Color)ColorConverter.ConvertFromString("#CCFF99");

            // 2. Define min/max brightness ratios
            const double minBrightnessRatio = 0.3; // Darkest step (darker color)
            const double maxBrightnessRatio = 1.0; // Brightest step (pure base color)

            // 3. Calculate step ratio (1.0 when step is 0, approaches minBrightnessRatio as step approaches totalSteps)
            double progress = (double)step / totalSteps;
            double ratio = maxBrightnessRatio - (progress * (maxBrightnessRatio - minBrightnessRatio));

            // 4. Apply ratio to RGB values to adjust color brightness
            byte r = (byte)(baseColor.R * ratio);
            byte g = (byte)(baseColor.G * ratio);
            byte b = (byte)(baseColor.B * ratio);

            // Ensure minimum values don't go below 0
            r = Math.Max((byte)0, r);
            g = Math.Max((byte)0, g);
            b = Math.Max((byte)0, b);

            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        /// <summary>
        /// Draw arrowhead
        /// </summary>
        private void DrawArrowhead(Canvas canvas, Point p1, Point p2, Brush color)
        {
            double angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
            double arrowLength = 15;
            double arrowAngle = Math.PI / 6; // 30 degrees

            Point p3 = new(p2.X - arrowLength * Math.Cos(angle + arrowAngle),
                            p2.Y - arrowLength * Math.Sin(angle + arrowAngle));
            Point p4 = new(p2.X - arrowLength * Math.Cos(angle - arrowAngle),
                            p2.Y - arrowLength * Math.Sin(angle - arrowAngle));

            canvas.Children.Add(new Line { X1 = p2.X, Y1 = p2.Y, X2 = p3.X, Y2 = p3.Y, Stroke = color, StrokeThickness = 4 });
            canvas.Children.Add(new Line { X1 = p2.X, Y1 = p2.Y, X2 = p4.X, Y2 = p4.Y, Stroke = color, StrokeThickness = 4 });
        }
    }
}