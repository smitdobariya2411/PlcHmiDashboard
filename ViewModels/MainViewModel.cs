using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using PlcHmiDashboard.Models;
using PlcHmiDashboard.Services;
using System.Linq;

namespace PlcHmiDashboard.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        /// <summary>Spot welds per body — must match SimulatedPlcService.WeldTarget / the PLC's target.</summary>
        public const int WeldTarget = 24;

        /// <summary>
        /// Takt time: the maximum time allowed per body to keep pace with the line's required
        /// output rate. Central concept in automotive/lean manufacturing — shown against the
        /// actual cycle time so you can see at a glance whether the station is keeping pace.
        /// </summary>
        public const double TaktTimeSeconds = 8.0;
        // Schematic (X,Y) position for each of the 24 spot welds, arranged as two rows across
        // the car body's footprint in the mimic diagram's coordinate space. Not real robot
        // kinematics — just enough to make the arm visibly move between weld locations.
        private static readonly (double X, double Y)[] WeldPointPositions = BuildWeldPointPositions();

        private static (double X, double Y)[] BuildWeldPointPositions()
        {
            var points = new (double X, double Y)[WeldTarget];
            for (int i = 0; i < WeldTarget; i++)
            {
                int col = i % 12;
                int row = i / 12;
                points[i] = (145 + col * 10, row == 0 ? 150 : 172);
            }
            return points;
        }

        private const double ArmHomeX = 238;
        private const double ArmHomeY = 132;
        private readonly IPlcService _plc;
        private DateTime _lastScanTime = DateTime.Now;
        private DateTime? _cycleStartTime;

        public MainViewModel(IPlcService plcService)
        {
            _plc = plcService;
            _plc.DataReceived += OnDataReceived;

            StartCommand = new RelayCommand(() => _plc.SendStart());
            StopCommand = new RelayCommand(() => _plc.SendStop());
            ResetCommand = new RelayCommand(() => _plc.SendReset());
            AckAlarmCommand = new RelayCommand<AlarmEntry>(alarm => alarm.IsAcknowledged = true);

            Trends = new ObservableCollection<TrendSeries> { WeldTipTempTrend, ClampPressureTrend, RobotSpeedTrend, WeldProgressTrend };

            _plc.Connect();
            ConnectionStatus = _plc.IsConnected ? "Connected" : "Disconnected";
        }

        // ---------------- Bindable process values ----------------

        private bool _clamped;
        public bool Clamped
        {
            get => _clamped;
            set { _clamped = value; OnPropertyChanged(); OnPropertyChanged(nameof(StateText)); OnPropertyChanged(nameof(ClampedBrush)); }
        }

        private bool _welding;
        public bool Welding
        {
            get => _welding;
            set { _welding = value; OnPropertyChanged(); OnPropertyChanged(nameof(StateText)); OnPropertyChanged(nameof(RobotStatusBrush)); }
        }

        private bool _indexing;
        public bool Indexing
        {
            get => _indexing;
            set { _indexing = value; OnPropertyChanged(); OnPropertyChanged(nameof(StateText)); }
        }

        private int _weldCount;
        public int WeldCount
        {
            get => _weldCount;
            set { _weldCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(WeldProgressPercent)); }
        }

        public double WeldProgressPercent => (WeldCount / (double)WeldTarget) * 100.0;
        public int WeldTargetValue => WeldTarget; // bindable wrapper — XAML can't bind directly to a const

        public ObservableCollection<WeldPointViewModel> WeldPoints { get; } =
                new ObservableCollection<WeldPointViewModel>(Enumerable.Range(1, WeldTarget).Select(n => new WeldPointViewModel(n)));

        private int _faultWeldIndex;
        public int FaultWeldIndex
        {
            get => _faultWeldIndex;
            private set { _faultWeldIndex = value; OnPropertyChanged(); }
        }

        private double _armTipX = ArmHomeX;
        public double ArmTipX
        {
            get => _armTipX;
            private set { _armTipX = value; OnPropertyChanged(); }
        }

        private double _armTipY = ArmHomeY;
        public double ArmTipY
        {
            get => _armTipY;
            private set { _armTipY = value; OnPropertyChanged(); }
        }
   
        private double _clampPressure;
        public double ClampPressure
        {
            get => _clampPressure;
            set { _clampPressure = value; OnPropertyChanged(); }
        }

        private double _robotSpeed;
        public double RobotSpeed
        {
            get => _robotSpeed;
            set { _robotSpeed = value; OnPropertyChanged(); }
        }

        private double _weldTipTemperature;
        public double WeldTipTemperature
        {
            get => _weldTipTemperature;
            set { _weldTipTemperature = value; OnPropertyChanged(); }
        }

        private bool _faultActive;
        public bool FaultActive
        {
            get => _faultActive;
            set { _faultActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(FaultBrush)); OnPropertyChanged(nameof(StateText)); }
        }

        private string _connectionStatus;
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set { _connectionStatus = value; OnPropertyChanged(); }
        }

        /// <summary>Derived, not a raw PLC bit — a real SCADA layer often composites raw signals like this.</summary>
        public bool StationRunning => Clamped || Welding || Indexing;

        /// <summary>Human-readable machine state, shown prominently on the Overview tab.</summary>
        public string StateText =>
            FaultActive ? "FAULT" :
            Welding ? "WELDING" :
            Indexing ? "INDEXING" :
            Clamped ? "CLAMPED" : "IDLE — NO BODY";

        // ---------------- Mimic-diagram helpers ----------------

        public Brush ClampedBrush => Clamped
            ? new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB))
            : new SolidColorBrush(Color.FromRgb(0x4A, 0x50, 0x58));

        public Brush RobotStatusBrush => Welding
            ? new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)) // orange = actively welding
            : new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6));

        public Brush FaultBrush => FaultActive
            ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))
            : new SolidColorBrush(Color.FromRgb(0x2C, 0x3E, 0x50));

        // ---------------- Production / KPI counters (derived client-side from raw signals) ----------------

        private int _bodiesProduced;
        public int BodiesProduced
        {
            get => _bodiesProduced;
            private set { _bodiesProduced = value; OnPropertyChanged(); }
        }

        private int _faultCount;
        public int FaultCount
        {
            get => _faultCount;
            private set { _faultCount = value; OnPropertyChanged(); }
        }

        private double _lastCycleTimeSeconds;
        public double LastCycleTimeSeconds
        {
            get => _lastCycleTimeSeconds;
            private set { _lastCycleTimeSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(TaktStatusText)); OnPropertyChanged(nameof(TaktStatusBrush)); }
        }

        /// <summary>"On pace" if the last completed body beat takt time, "Behind takt" if it didn't.</summary>
        public string TaktStatusText => _lastCycleTimeSeconds <= 0 ? "—"
            : _lastCycleTimeSeconds <= TaktTimeSeconds ? "ON PACE" : "BEHIND TAKT";

        public Brush TaktStatusBrush => _lastCycleTimeSeconds <= 0
            ? new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6))
            : _lastCycleTimeSeconds <= TaktTimeSeconds
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
                : new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C));

        private double _runSeconds;
        private double _downSeconds;
        private double _idleSeconds;

        public string UptimeText => TimeSpan.FromSeconds(_runSeconds).ToString(@"mm\:ss");
        public string DowntimeText => TimeSpan.FromSeconds(_downSeconds).ToString(@"mm\:ss");
        public string IdleTimeText => TimeSpan.FromSeconds(_idleSeconds).ToString(@"mm\:ss");

        private double _availabilityPercent;
        public double AvailabilityPercent
        {
            get => _availabilityPercent;
            private set { _availabilityPercent = value; OnPropertyChanged(); }
        }

        // ---------------- Alarm log ----------------

        public ObservableCollection<AlarmEntry> AlarmLog { get; } = new ObservableCollection<AlarmEntry>();
        public RelayCommand<AlarmEntry> AckAlarmCommand { get; }

        private void LogEvent(string message, AlarmSeverity severity)
        {
            AlarmLog.Insert(0, new AlarmEntry { Timestamp = DateTime.Now, Message = message, Severity = severity });
            while (AlarmLog.Count > 100) AlarmLog.RemoveAt(AlarmLog.Count - 1);
        }

        // ---------------- Trend charts ----------------

        public TrendSeries WeldTipTempTrend { get; } = new TrendSeries("Weld Tip Temperature", "°C", Brushes.Gold, 0, 450);
        public TrendSeries ClampPressureTrend { get; } = new TrendSeries("Clamp Pressure", "bar", Brushes.DeepSkyBlue, 0, 8);
        public TrendSeries RobotSpeedTrend { get; } = new TrendSeries("Robot TCP Speed", "mm/s", Brushes.MediumSpringGreen, 0, 1000);
        public TrendSeries WeldProgressTrend { get; } = new TrendSeries("Weld Progress", "%", Brushes.OrangeRed, 0, 100);

        public ObservableCollection<TrendSeries> Trends { get; }

        // ---------------- Live tag browser ----------------

        public ObservableCollection<TagRow> TagRows { get; } = new ObservableCollection<TagRow>();

        // ---------------- Commands ----------------

        public RelayCommand StartCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand ResetCommand { get; }

        // ---------------- PLC data pump ----------------

        private void OnDataReceived(object sender, PlcDataSnapshot snapshot)
        {
            // DataReceived fires on the timer thread, not the UI thread — every UI update
            // must be marshalled back with Dispatcher.Invoke, or WPF will throw.
            Application.Current?.Dispatcher.Invoke(() => Apply(snapshot));
        }

        private void Apply(PlcDataSnapshot snapshot)
        {
            var now = DateTime.Now;
            double elapsed = (now - _lastScanTime).TotalSeconds;
            _lastScanTime = now;

            bool wasFault = FaultActive;
            bool wasWelding = Welding;
            bool wasIndexing = Indexing;
            bool wasClamped = Clamped;
            int previousWeldCount = WeldCount;

            Clamped = snapshot.Clamped;
            Welding = snapshot.Welding;
            Indexing = snapshot.Indexing;
            WeldCount = snapshot.WeldCount;
            ClampPressure = snapshot.ClampPressure;
            RobotSpeed = snapshot.RobotSpeed;
            WeldTipTemperature = snapshot.WeldTipTemperature;
            FaultActive = snapshot.FaultActive;
            OnPropertyChanged(nameof(StationRunning));

            // --- cycle timing, feeds the Takt time comparison ---
            // Capture the start time BEFORE it might get overwritten below — indexing-finishes
            // and next-weld-starts can land in the same scan tick, and we need the OLD cycle's
            // start time to compute how long it took, not the new cycle's.
            DateTime? completingCycleStartTime = _cycleStartTime;

            if (Welding && !wasWelding)
                _cycleStartTime = now;

            // --- time-in-state accounting, feeds the Production/KPI tab ---
            if (FaultActive) _downSeconds += elapsed;
            else if (StationRunning) _runSeconds += elapsed;
            else _idleSeconds += elapsed;

            double totalTracked = _runSeconds + _downSeconds + _idleSeconds;
            AvailabilityPercent = totalTracked > 0 ? (_runSeconds / totalTracked) * 100.0 : 0;
            OnPropertyChanged(nameof(UptimeText));
            OnPropertyChanged(nameof(DowntimeText));
            OnPropertyChanged(nameof(IdleTimeText));

            // --- production counters: a body is "produced" once indexing finishes cleanly ---
            if (wasIndexing && !Indexing && !FaultActive)
            {
                BodiesProduced++;
                if (completingCycleStartTime.HasValue)
                    LastCycleTimeSeconds = (now - completingCycleStartTime.Value).TotalSeconds;
                LogEvent($"Body #{BodiesProduced} complete — {WeldTarget} welds, {LastCycleTimeSeconds:0.0}s ({TaktStatusText})", AlarmSeverity.Info);
            }

            // --- trend charts ---
            WeldTipTempTrend.AddSample(WeldTipTemperature);
            ClampPressureTrend.AddSample(ClampPressure);
            RobotSpeedTrend.AddSample(RobotSpeed);
            WeldProgressTrend.AddSample(WeldProgressPercent);

            // --- tag browser: refresh every raw signal, like a real SCADA diagnostic screen ---
            RefreshTagRows(now);

            // --- alarm log transitions ---
            // --- individual weld points: reset for a new body, then update states each scan ---
            if (Clamped && !wasClamped)
            {
                foreach (var point in WeldPoints) point.State = WeldPointState.Pending;
            }

            if (FaultActive && !wasFault)
                FaultWeldIndex = previousWeldCount + 1;

            foreach (var point in WeldPoints)
            {
                if (FaultActive && point.Number == FaultWeldIndex)
                    point.State = WeldPointState.Fault;
                else if (point.Number <= WeldCount)
                    point.State = WeldPointState.Complete;
                else if (point.Number == WeldCount + 1 && Welding)
                    point.State = WeldPointState.InProgress;
                else
                    point.State = WeldPointState.Pending;
            }

            var armTarget = FaultActive
             ? WeldPointPositions[Math.Clamp(FaultWeldIndex - 1, 0, WeldTarget - 1)]
            : Welding
             ? WeldPointPositions[Math.Min(WeldCount, WeldTarget - 1)]
             : (ArmHomeX, ArmHomeY);

            if (armTarget.Item1 != ArmTipX || armTarget.Item2 != ArmTipY)
            {
                ArmTipX = armTarget.Item1;
                ArmTipY = armTarget.Item2;
            }


            // --- alarm log transitions ---
            if (FaultActive && !wasFault)
            {
                FaultCount++;
                LogEvent($"Weld fault tripped at weld point {FaultWeldIndex}/{WeldTarget} — station halted, body held in fixture", AlarmSeverity.Fault);
            }
            if (!FaultActive && wasFault)
                LogEvent("Fault cleared by operator Reset", AlarmSeverity.Info);

            if (Clamped && !wasClamped)
                LogEvent("Body arrived — fixture clamping", AlarmSeverity.Info);

            if (Welding && !wasWelding)
                LogEvent("Weld sequence started", AlarmSeverity.Info);

            if (Indexing && !wasIndexing)
                LogEvent($"Weld sequence complete ({WeldTarget}/{WeldTarget}) — indexing", AlarmSeverity.Info);
        }

        private void RefreshTagRows(DateTime timestamp)
        {
            void Set(string name, string value, string unit)
            {
                var row = default(TagRow);
                foreach (var r in TagRows) { if (r.Name == name) { row = r; break; } }

                if (row == null)
                {
                    row = new TagRow { Name = name, Unit = unit };
                    TagRows.Add(row);
                }
                row.Value = value;
                row.LastUpdate = timestamp;
            }

            Set("Clamped", Clamped.ToString(), "bool");
            Set("Welding", Welding.ToString(), "bool");
            Set("Indexing", Indexing.ToString(), "bool");
            Set("FaultActive", FaultActive.ToString(), "bool");
            Set("WeldCount", $"{WeldCount}/{WeldTarget}", "count");
            Set("ClampPressure", ClampPressure.ToString("0.00"), "bar");
            Set("RobotSpeed", RobotSpeed.ToString("0"), "mm/s");
            Set("WeldTipTemperature", WeldTipTemperature.ToString("0.0"), "°C");
            Set("BodiesProduced", BodiesProduced.ToString(), "count");
            Set("FaultCount", FaultCount.ToString(), "count");
            Set("LastCycleTime", LastCycleTimeSeconds.ToString("0.0"), "s");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
