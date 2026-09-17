using System;
using System.ComponentModel;

namespace PlcHmiDashboard.Models
{
    /// <summary>
    /// One row in the live tag browser - mirrors how real SCADA/HMI diagnostic screens show
    /// every raw signal with its current value, unit, and last-update time.
    /// Implements INotifyPropertyChanged so the DataGrid cells update live as values change,
    /// without needing to rebuild the whole row collection every scan.
    /// </summary>
    public class TagRow : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Quality { get; set; } = "Good";

        private string _value;
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }

        private DateTime _lastUpdate;
        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set { _lastUpdate = value; OnPropertyChanged(nameof(LastUpdate)); OnPropertyChanged(nameof(LastUpdateText)); }
        }

        public string LastUpdateText => LastUpdate.ToString("HH:mm:ss.fff");

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
