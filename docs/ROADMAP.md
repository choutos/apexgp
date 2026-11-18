# ApexGP — Roadmap

## M0 — Simulation core (current)
Pure C#, headless, unit-tested.
- [x] Rigid-body chassis (6-DOF, world-frame integration, body-frame angular rates)
- [x] Pacejka Magic Formula tire model (lateral + longitudinal, load sensitivity,
      combined-slip friction ellipse)
- [x] Independent suspension (spring / damper / ARB / bump stops), raycast contact
- [x] Drivetrain: torque curve engine, 5-speed auto gearbox, viscous clutch with
      slip/lock model, RWD axle coupling, engine braking, ABS + progressive TCS
- [x] Acceptance tests green — 12/12 (idle, 0-100, braking, top speed, cornering,
      120 s stability, weight transfer)

**Done when**: full headless test suite passes; no NaN in 120 s mixed driving.

## M0.5 — Godot shell
- Godot 4 .NET project; sim core drives a simple test car on a flat plane
- Chase camera, gamepad + keyboard input, basic HUD (speed, gear, RPM)
- First visual pass: PS1-inspired low-poly look with modern lighting

## M1 — Track pipeline + racing
- Spline-driven track builder (control points -> mesh, collision, AI racing line)
- Lap timing, sector times, replay ghosts
- AI opponents (racing-line followers, per-driver pace parameters)
- Force feedback via SDL haptics

## M2 — Career loop MVP
- License school (timed gate challenges)
- Credits, race series with entry requirements, prize cars
- Dealership (new + used with condition/mileage) and tuning shop
  (engine stages, turbo, gearing, LSD, weight reduction, aero)

## M3 — Content + polish (current)
- [x] Procedural engine audio (RPM-pitched synthesis)
- [ ] PS1-style visual polish pass (low-poly detail, dithering, higher cars)
- [ ] More cars across classes, second circuit content
- [ ] Gamepad force feedback tuning

## Done
- M0 (sim core, 12 tests) — see git history
- M0.5 (Godot shell) — see git history
- M1a (track pipeline) / M1b (race mode) — see git history
- M2a (career: dealership, tuning, licences, events) — see git history
