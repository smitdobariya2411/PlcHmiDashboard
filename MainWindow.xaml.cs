using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using PlcHmiDashboard.Services;
using PlcHmiDashboard.ViewModels;

namespace PlcHmiDashboard
{
    public partial class MainWindow : Window
    {
        private readonly IPlcService _plcService;
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            _plcService = new SimulatedPlcService();
            // _plcService = new Sharp7PlcService(ip: "127.0.0.1", rack: 0, slot: 0);

            _viewModel = new MainViewModel(_plcService);
            DataContext = _viewModel;

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.ArmTipY))
            {
                AnimateArmTo(_viewModel.ArmTipX, _viewModel.ArmTipY);
            }
        }

        private void AnimateArmTo(double targetX, double targetY)
        {
            var duration = TimeSpan.FromMilliseconds(350);

            ArmSegment2.BeginAnimation(System.Windows.Shapes.Line.X2Property, new DoubleAnimation(targetX, duration));
            ArmSegment2.BeginAnimation(System.Windows.Shapes.Line.Y2Property, new DoubleAnimation(targetY, duration));

            ArmTipEllipse.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(targetX - 8, duration));
            ArmTipEllipse.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(targetY - 8, duration));

            WeldSpark.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(targetX - 14, duration));
            WeldSpark.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(targetY - 14, duration));
        }

        protected override void OnClosed(EventArgs e)
        {
            _plcService?.Dispose();
            base.OnClosed(e);
        }
    }
}