using System.ComponentModel;
using System.Windows.Media;

namespace PlcHmiDashboard.ViewModels
{
    public enum WeldPointState
    {
        Pending,
        InProgress,
        Complete,
        Fault
    }

    public class WeldPointViewModel : INotifyPropertyChanged
    {
        public int Number { get; }

        private WeldPointState _state = WeldPointState.Pending;
        public WeldPointState State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(nameof(State)); OnPropertyChanged(nameof(Fill)); }
        }

        public Brush Fill => State switch
        {
            WeldPointState.Complete => new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71)),
            WeldPointState.InProgress => new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)),
            WeldPointState.Fault => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
            _ => new SolidColorBrush(Color.FromRgb(0x4A, 0x50, 0x58))
        };

        public WeldPointViewModel(int number)
        {
            Number = number;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}