using System;
using System.Timers;
using Sharp7;

namespace PlcHmiDashboard.Services
{
    /// <summary>
    /// Connects to a real or PLCSIM-simulated Siemens S7-1200/1500 using the Sharp7 library.
    ///
    /// Expects a Data Block "DB1" in the PLC project with "Optimized block access" DISABLED
    /// (right-click DB1 -> Properties -> Attributes -> untick "Optimized block access"),
    /// laid out exactly like this (byte.bit offsets):
    ///
    ///   Offset 0.0   Clamped           Bool
    ///   Offset 0.1   Welding           Bool
    ///   Offset 0.2   Indexing          Bool
    ///   Offset 0.3   FaultActive       Bool
    ///   Offset 0.4   StartCommand      Bool   (written by the HMI, read+reset by the PLC logic)
    ///   Offset 0.5   StopCommand       Bool   (written by the HMI)
    ///   Offset 0.6   ResetCommand      Bool   (written by the HMI)
    ///   Offset 2.0   ClampPressure     Real   (4 bytes) — bar
    ///   Offset 6.0   RobotSpeed        Real   (4 bytes) — mm/s
    ///   Offset 10.0  WeldTipTemp       Real   (4 bytes) — °C
    ///   Offset 14.0  WeldCount         Int    (2 bytes) — spot welds completed this cycle
    ///
    /// Total DB size: 16 bytes.
    ///
    /// Note: production counters (bodies welded, cycle time vs. takt time, availability) are
    /// deliberately NOT read from the PLC here - the ViewModel derives them client-side by
    /// watching these raw signals over time, exactly like a real SCADA/HMI layer sits on top
    /// of raw PLC tags rather than duplicating that logic in ladder code.
    /// </summary>
    public class Sharp7PlcService : IPlcService
    {
        private readonly S7Client _client = new S7Client();
        private readonly Timer _pollTimer;
        private readonly string _ip;
        private readonly int _rack;
        private readonly int _slot;

        private const int DbNumber = 1;
        private const int DbSize = 16; // bytes, must match the DB layout above

        public bool IsConnected => _client.Connected;
        public event EventHandler<PlcDataSnapshot> DataReceived;

        /// <param name="ip">
        /// 127.0.0.1 for PLCSIM running on the same PC. Use the real PLC's IP for hardware.
        /// </param>
        /// <param name="rack">Usually 0.</param>
        /// <param name="slot">
        /// S7-1500 / PLCSIM: usually 0. S7-1200: usually 1. Check your PLC's HW config if the
        /// connection fails — this is the most common cause of a failed connect.
        /// </param>
        public Sharp7PlcService(string ip = "127.0.0.1", int rack = 0, int slot = 0)
        {
            _ip = ip;
            _rack = rack;
            _slot = slot;

            _pollTimer = new Timer(500);
            _pollTimer.Elapsed += (s, e) => Poll();
        }

        public void Connect()
        {
            int result = _client.ConnectTo(_ip, _rack, _slot);
            if (result != 0)
                throw new Exception($"Sharp7 connect failed: {_client.ErrorText(result)}");

            _pollTimer.Start();
        }

        public void Disconnect()
        {
            _pollTimer.Stop();
            if (_client.Connected) _client.Disconnect();
        }

        private void Poll()
        {
            byte[] buffer = new byte[DbSize];
            int result = _client.DBRead(DbNumber, 0, DbSize, buffer);
            if (result != 0) return; // transient read error - just skip this cycle

            var snapshot = new PlcDataSnapshot
            {
                Clamped = S7.GetBitAt(buffer, 0, 0),
                Welding = S7.GetBitAt(buffer, 0, 1),
                Indexing = S7.GetBitAt(buffer, 0, 2),
                FaultActive = S7.GetBitAt(buffer, 0, 3),
                ClampPressure = S7.GetRealAt(buffer, 2),
                RobotSpeed = S7.GetRealAt(buffer, 6),
                WeldTipTemperature = S7.GetRealAt(buffer, 10),
                WeldCount = S7.GetIntAt(buffer, 14)
            };

            DataReceived?.Invoke(this, snapshot);
        }

        public void SendStart() => PulseBit(bitOffset: 4);
        public void SendStop() => PulseBit(bitOffset: 5);
        public void SendReset() => PulseBit(bitOffset: 6);

        /// <summary>
        /// Sets a single command bit in byte 0 of DB1. The PLC ladder logic is responsible for
        /// reading and then clearing it (a standard one-scan "pulse" command pattern), 
        /// </summary>
        private void PulseBit(int bitOffset)
        {
            if (!_client.Connected) return;

            byte[] buffer = new byte[1];
            _client.DBRead(DbNumber, 0, 1, buffer);
            S7.SetBitAt(buffer, 0, bitOffset, true);
            _client.DBWrite(DbNumber, 0, 1, buffer);
        }

        public void Dispose()
        {
            _pollTimer?.Stop();
            _pollTimer?.Dispose();
            if (_client.Connected) _client.Disconnect();
        }
    }
}
