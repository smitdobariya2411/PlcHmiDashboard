using System;

namespace PlcHmiDashboard.Services
{
    /// <summary>
    /// A snapshot of all process values read from the PLC (or simulator) on one scan cycle.
    /// Modeled on a robotic spot-welding cell on an automotive Body-in-White (BIW) line:
    /// a car body is clamped in a fixture, a robot performs a set number of spot welds,
    /// the fixture releases, and the conveyor indexes the body to the next station.
    /// </summary>
    public class PlcDataSnapshot
    {
        public bool Clamped { get; set; }              // fixture clamped on the car body
        public bool Welding { get; set; }               // robot actively welding
        public bool Indexing { get; set; }              // conveyor advancing body to next station
        public int WeldCount { get; set; }               // spot welds completed this cycle
        public double ClampPressure { get; set; }         // bar, pneumatic clamp pressure
        public double RobotSpeed { get; set; }             // mm/s, robot TCP (tool center point) speed
        public double WeldTipTemperature { get; set; }      // °C, welding electrode tip temperature
        public bool FaultActive { get; set; }
    }

    /// <summary>
    /// Common contract for anything that can act as our "PLC" — a real S7 PLC (via Sharp7 + PLCSIM),
    /// or a pure software simulator. The ViewModel only ever talks to this interface, so swapping
    /// the implementation is a one-line change.
    /// </summary>
    public interface IPlcService : IDisposable
    {
        bool IsConnected { get; }
        event EventHandler<PlcDataSnapshot> DataReceived;

        void Connect();
        void Disconnect();
        void SendStart();
        void SendStop();
        void SendReset();
    }
}
