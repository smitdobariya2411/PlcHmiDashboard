	# Body Shop Station 3 - Robotic Spot-Welding Cell (SCADA/HMI)

A WPF desktop SCADA/HMI application modeling a real automotive process: **a robotic
spot-welding cell on a Body-in-White (BIW) assembly line**. A car body arrives, the fixture
clamps it, a robot performs a target number of spot welds, the fixture releases, and the
conveyor indexes the body out to the next station — repeating continuously, with occasional
weld faults, exactly like a real body shop station.

It can run in two modes:
1. **Simulated mode** (default) — pure C#, no PLC or hardware required.
2. **Real PLC mode** — connects via the Sharp7 library to a Siemens S7-1200/1500 PLC, either
   simulated in PLCSIM or a real physical PLC  network.




- **Clamp pressure (bar)** — pneumatic clamps are everywhere on a body line
- **Robot TCP speed (mm/s)** — how fast the robot's tool tip is moving
- **Weld tip temperature (°C)** — electrode wear/overheating is a real weld-quality concern
- **Weld count vs. target** — how many of the programmed spot welds are done this cycle
- **Takt time** — the defining lean-manufacturing concept: the maximum time allowed per unit
  to keep pace with line demand. The Production tab compares each completed body's actual
  cycle time against a target takt time and flags whether the station is "on pace" or "behind."

## What's on each tab

- **Overview** — process mimic (car body + clamp status, schematic robot arm, fault lamp,
  clamp pressure / robot speed / weld tip temp readouts), a weld-progress bar, quick KPI cards,
  and a live recent-events feed.
- **Trends** — four live-scrolling charts (weld tip temperature, clamp pressure, robot TCP
  speed, weld progress %), each with its own color and current value.
- **Alarms** — a full alarm/event table with severity (Info/Warning/Fault) and an operator
  Acknowledge workflow, the way a real alarm management screen behaves.
- **Production** — bodies produced, fault count, last cycle time, uptime/downtime/idle time,
  an Availability progress bar, and a dedicated Takt time comparison.
- **Tag Browser** — every raw signal from the PLC/simulator in one live table with value, unit,
  and last-update timestamp, the way a SCADA diagnostics screen lets you inspect raw tags.




