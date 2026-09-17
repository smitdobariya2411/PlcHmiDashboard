using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace PlcHmiDashboard.ViewModels
{
    /// <summary>
    /// One live-scrolling trend line: a name, a color, a value range to scale against, and the
    /// scaled points a Polyline binds to directly. Each PLC/simulator value gets its own instance
    /// (temperature, pressure, motor speed, tank level) so the Trends tab can show several at once.
    /// </summary>
    public class TrendSeries : INotifyPropertyChanged
    {
        private const double ChartWidth = 560;
        private const double ChartHeight = 90;

        private readonly double _minValue;
        private readonly double _maxValue;
        private readonly int _maxPoints;

        public string Name { get; }
        public string Unit { get; }
        public Brush Stroke { get; }
        public PointCollection Points { get; } = new PointCollection();

        private double _latestValue;
        public double LatestValue
        {
            get => _latestValue;
            private set { _latestValue = value; OnPropertyChanged(nameof(LatestValue)); }
        }

        public TrendSeries(string name, string unit, Brush stroke, double minValue, double maxValue, int maxPoints = 120)
        {
            Name = name;
            Unit = unit;
            Stroke = stroke;
            _minValue = minValue;
            _maxValue = maxValue;
            _maxPoints = maxPoints;
        }

        public void AddSample(double value)
        {
            LatestValue = value;

            double clamped = Math.Clamp(value, _minValue, _maxValue);
            const double margin = 4;
            double usableHeight = ChartHeight - margin * 2;
            double y = margin + usableHeight - (clamped - _minValue) / (_maxValue - _minValue) * usableHeight;

            if (Points.Count >= _maxPoints) Points.RemoveAt(0);
            Points.Add(new Point(0, y)); // x corrected for every point below

            // Re-space all x-coordinates evenly every sample. Cheap at 120 points, and it avoids
            // any jump/wraparound artifact once the buffer is full and old points drop off.
            double spacing = ChartWidth / (double)(_maxPoints - 1);
            for (int i = 0; i < Points.Count; i++)
            {
                var p = Points[i];
                Points[i] = new Point(i * spacing, p.Y);
            }

            // Polyline is not itself a Freezable, so mutating Points in place isn't guaranteed
            // to trigger a redraw on its own - explicitly notify so WPF re-reads and redraws.
            OnPropertyChanged(nameof(Points));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
