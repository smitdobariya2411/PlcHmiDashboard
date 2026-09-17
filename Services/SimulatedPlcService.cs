using System;
using System.Timers;

namespace PlcHmiDashboard.Services
{
    /// <summary>
    /// A software-only stand-in for a PLC, modeling a robotic spot-welding cell (Station 3 on a
    /// Body-in-White assembly line): a car body arrives, the fixture clamps it, a robot performs
    /// a target number of spot welds, the fixture releases, and the conveyor indexes the body out
    /// while the next one arrives. Repeats continuously while Start is engaged, with a small
    /// random chance of a weld fault (simulating a real weld-current or clamp-pressure fault).
    ///
    /// Requires no hardware, no TIA Portal, and no PLCSIM — use it to build and demo the HMI
    /// first. Later, swap to Sharp7PlcService without changing the UI.
    /// </summary>
    public class SimulatedPlcService : IPlcService
    {
        public const int WeldTarget = 24; // spot welds per body — matches MainViewModel.WeldTarget

        private readonly Timer _scanTimer;
        private readonly Random _rng = new Random();

        private bool _autoRun;      // true while Start is engaged (continuous cycling)
        private bool _clamped;
        private bool _welding;
        private bool _indexing;
        private bool _fault;
        private int _weldCount;
        private int _indexingScansRemaining;
        private int _clampSettleScansRemaining;

        private double _clampPressure = 1.0;
        private double _robotSpeed;
        private double _weldTipTemp = 20.0;

        public bool IsConnected { get; private set; }
        public event EventHandler<PlcDataSnapshot> DataReceived;

        public SimulatedPlcService()
        {
            // 500ms tick approximates a realistic PLC scan/poll rate for an HMI
            _scanTimer = new Timer(500);
            _scanTimer.Elapsed += OnScan;
        }

        public void Connect()
        {
            IsConnected = true;
            _scanTimer.Start();
        }

        public void Disconnect()
        {
            IsConnected = false;
            _scanTimer.Stop();
        }

        public void SendStart()
        {
            _autoRun = true;
            if (!_fault && !_clamped && !_welding && !_indexing)
                BeginWeldCycle();
        }

        public void SendStop()
        {
            _autoRun = false;
            // Let an in-progress weld/index finish rather than freezing mid-cycle.
        }

        public void SendReset()
        {
            _fault = false;
            _clamped = false;
            _welding = false;
            _indexing = false;
            _weldCount = 0;
            if (_autoRun) BeginWeldCycle();
        }

        private void BeginWeldCycle()
        {
            _clamped = true;
            _welding = false; // not yet - see the settle delay in OnScan
            _weldCount = 0;
            _clampSettleScansRemaining = 2; // ~1 second: body visibly clamped before the robot starts moving
        }
        private void OnScan(object sender, ElapsedEventArgs e)
        {
            // Small random chance of a weld fault while actively welding - simulates a real
            // equipment trip (weld current fault, electrode wear, clamp pressure loss, etc.)
            if (_welding && !_fault && _rng.NextDouble() < 0.004)
            {
                _fault = true;
                _welding = false;
            }

            // Body is clamped but the robot hasn't started yet - a brief, visible "settle" beat
            // so the arrival/clamp is clearly seen before the arm starts moving to weld points.
            if (_clamped && !_welding && !_indexing && !_fault)
            {
                if (_clampSettleScansRemaining > 0)
                    _clampSettleScansRemaining--;
                else
                    _welding = true;
            }

            if (_welding)
            {
                _weldCount += 2; // ~2 spot welds per scan -> full cycle in a few seconds for demo purposes
                _robotSpeed = Approach(_robotSpeed, 850, 120);
                _clampPressure = Approach(_clampPressure, 5.5, 0.4) + Noise(0.08);
                _weldTipTemp = Approach(_weldTipTemp, 380, 15) + Noise(2.0);

                if (_weldCount >= WeldTarget)
                {
                    _weldCount = WeldTarget;
                    _welding = false;
                    _clamped = false;
                    _indexing = true;
                    _indexingScansRemaining = 2; // ~1 second of indexing motion
                }
            }
            else if (_indexing)
            {
                _robotSpeed = Approach(_robotSpeed, 0, 200);
                _clampPressure = Approach(_clampPressure, 1.0, 0.5);
                _weldTipTemp = Approach(_weldTipTemp, 340, 10);

                _indexingScansRemaining--;
                if (_indexingScansRemaining <= 0)
                {
                    _indexing = false;
                    if (_autoRun && !_fault) BeginWeldCycle();
                }
            }
            else if (_clamped) // settling: clamped but robot hasn't started moving yet
            {
                _clampPressure = Approach(_clampPressure, 5.5, 0.6);
                _robotSpeed = Approach(_robotSpeed, 0, 200);
                _weldTipTemp = Approach(_weldTipTemp, 340, 10);
            }
            else if (_fault)
            {
                _robotSpeed = Approach(_robotSpeed, 0, 200);
                _clampPressure = Approach(_clampPressure, 0.5, 0.2);
                _weldTipTemp = Approach(_weldTipTemp, 20, 3);
            }
            else // idle - no body in the fixture, nothing commanded
            {
                _robotSpeed = Approach(_robotSpeed, 0, 200);
                _clampPressure = Approach(_clampPressure, 1.0, 0.2);
                _weldTipTemp = Approach(_weldTipTemp, 20, 3);
            }

            var snapshot = new PlcDataSnapshot
            {
                Clamped = _clamped,
                Welding = _welding,
                Indexing = _indexing,
                WeldCount = _weldCount,
                ClampPressure = Math.Max(0, _clampPressure),
                RobotSpeed = Math.Max(0, _robotSpeed),
                WeldTipTemperature = Math.Max(0, _weldTipTemp),
                FaultActive = _fault
            };

            DataReceived?.Invoke(this, snapshot);
        }

        /// <summary>Moves a value toward a target by at most maxStep per scan - smooth ramps instead of instant jumps.</summary>
        private static double Approach(double current, double target, double maxStep)
        {
            double diff = target - current;
            if (Math.Abs(diff) <= maxStep) return target;
            return current + Math.Sign(diff) * maxStep;
        }

        private double Noise(double amplitude) => (_rng.NextDouble() * 2 - 1) * amplitude;

        public void Dispose()
        {
            _scanTimer?.Stop();
            _scanTimer?.Dispose();
        }
    }
}
