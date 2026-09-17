using System;
using System.ComponentModel;

namespace PlcHmiDashboard.Models
{
    public enum AlarmSeverity
    {
        Info,
        Warning,
        Fault
    }

    /// <summary>
    /// A single row in the HMI's alarm/event log. Implements INotifyPropertyChanged so the
    /// Acknowledged flag updates its row in the UI live when the operator clicks Ack.
    /// </summary>
    public class AlarmEntry : INotifyPropertyChanged
    {
        public DateTime Timestamp { get; set; }
        public string Message { get; set; }
        public AlarmSeverity Severity { get; set; }

        private bool _isAcknowledged;
        public bool IsAcknowledged
        {
            get => _isAcknowledged;
            set { _isAcknowledged = value; OnPropertyChanged(nameof(IsAcknowledged)); OnPropertyChanged(nameof(RequiresAckAndUnacked)); }
        }

        public string SeverityText => Severity.ToString().ToUpperInvariant();
        public string TimeText => Timestamp.ToString("HH:mm:ss");

        /// <summary>Only Warning/Fault rows need acknowledgement — Info rows are just log entries.</summary>
        public bool RequiresAck => Severity != AlarmSeverity.Info;

        /// <summary>True while an Ack button should still be shown for this row.</summary>
        public bool RequiresAckAndUnacked => RequiresAck && !IsAcknowledged;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
