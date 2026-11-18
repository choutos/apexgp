# Physics model — M0 spec

Conventions: right-handed, **Y up, -Z forward, +X right** (Godot convention).
Units: SI (m, s, kg, N, Nm, rad). Physics sub-step: 1/240 s (host decouples
render). Flat ground plane at y = 0 (surface model deferred).

## Chassis
Rigid body, mass + box-approx inertia (pitch/roll/yaw). Semi-implicit Euler.
Angular state kept in body frame, includes gyroscopic coupling term
(`ω̇ = I⁻¹(τ − ω × Iω)`).

## Tires — Pacejka Magic Formula (normalized)
`F = μ·Fz·sin(C·atan(Bx − E(Bx − atan(Bx))))`, with `x = slip/peak`:

| axis          | B   | C    | E   | peak          |
|---------------|-----|------|-----|---------------|
| lateral       | 30  | 1.30 | 0.5 | ≈ 7.2°        |
| longitudinal  | 17  | 1.65 | 0.6 | κ ≈ 0.11      |

- MF is odd-symmetric: sign follows slip direction.
- Load sensitivity: `μ(Fz) = μ₀·(1 − 1.5e-5·(Fz − 4000))`, clamped to
  `[0.7·μ₀, μ₀]`.
- Combined slip: friction-ellipse normalization (magnitude scaled to ≤ μ·Fz).

## Suspension
Per wheel: linear spring + linear damper (compression-velocity based),
anti-roll bar per axle (force ∝ left/right compression difference), bump
stops beyond travel. Compression from raycast along chassis -up axis to the
ground plane; compression-velocity clamped (±10 m/s) to avoid launch spikes.

## Drivetrain
- Engine: piecewise-linear torque curve, friction `45 + 0.05·ω`, soft limiter
  at 0.98× redline, hard cut at redline. Idle governor keeps ~800 rpm.
- Clutch: two-state (slipping / locked). Slipping: engine free-revs with a
  launch floor (~1500 rpm under load), transmits `min(T_engine, capacity)`.
  Locked: rigid coupling `ω_e = ω_axle·ratio`; breaks on stall threat or
  near-zero-speed park. Lock when mismatch < 5%.
- Gearbox: 5 ratios + final drive, 0.92 efficiency, auto shift at 6900 up /
  2600 down with 0.3 s torque cut.
- Driven axle integrated as a unit when locked (`J = 2·I_wheel + I_e·ratio²`);
  independent wheels when slipping (open-diff-like behaviour, v0 approximation).
- ABS: reduces brake torque ×0.25 when slip < −0.35. TCS: cuts drive torque
  ×0.35 when slip ratio > 0.3.

## v0 known limitations (tracked, by design)
- Driven axle treated as locked pair when clutch is locked (spool effect in
  corners) — proper open/LSD diff planned in M1.
- No camber/toe/caster, no aerodynamic downforce, flat ground only,
  50/50 static weight distribution (symmetric mounts).
- Low-speed longitudinal slip uses a fixed denominator (2 m/s) — standard
  pragmatic fix; revisit with relaxation-length model later.
