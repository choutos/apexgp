# ApexGP

*(working title — final name TBD)*

An open-source, clean-room racing simulator built in the spirit of late-90s
console GT-style games: an approachable sim handling model, licence tests, a
car-buying economy, tuning and race events — with **100% original code and
assets**. No content, code, data, or design documents from any commercial
title are referenced, derived from, or shipped with this project.

## Principles

1. **Clean-room, always.** Fictional marques, original circuits, original name.
   Public-domain vehicle-dynamics literature is the only reference material.
2. **Sim-first, accessible by default.** Honest physics (Pacejka-family tire
   model, weight transfer, drivetrain) with GT-era assists (ABS / TCS)
   on by default and fully configurable.
3. **Data-driven content.** Cars and tracks are plain data — moddable
   and community-extensible from day one.
4. **Vertical slice discipline.** Driving feel outranks content volume until
   the feel is right.

## Current state (v0.1 "Vertical Slice+")

- **Simulation core** (`src/ApexGP.Core`, pure C#): 6-DOF chassis, Pacejka tire
  model with load sensitivity and combined-slip ellipse, independent
  suspension (spring/damper/ARB/bump stops), torque-curve engine, viscous
  clutch with launch behaviour, 5-speed auto gearbox, ABS + progressive TCS.
- **Track pipeline**: Catmull-Rom circuit builder with racing line and a
  braking-aware speed profile; tarmac/kerb/grass grip model; lap timing with
  cut detection. First circuit: *Sunrise Loop* (1.38 km).
- **AI**: racing-line followers with per-driver pace, grid starts, live
  positions, results.
- **Career**: credits, garage, dealership (4 original models), tuning shop
  (7 parts with real physics effects), licence school (human-driven gate
  tests), event calendar with entry requirements and payouts. Profile
  persists to JSON.
- **Godot 4.7 shell** (`engine/`): 3D race with chase camera, HUD
  (speed/gear/rpm/position/lap), procedural engine audio, keyboard +
  gamepad input.

## Build & test

```sh
# sim core tests (headless, no engine needed)
dotnet build tests/ApexGP.Tests/ApexGP.Tests.csproj --no-incremental
dotnet run --no-build --project tests/ApexGP.Tests     # exit 0 = all pass

# Godot shell (needs Godot 4.7 .NET build)
godot --headless --path engine --import
godot --path engine            # play!
```

`APEXGP_SMOKE=1 godot --headless --path engine --quit-after 700` runs an
automated smoke race (acceleration + braking assertions).

## Layout

```
docs/            design, roadmap, physics model spec
src/ApexGP.Core  sim core + game logic (tracks, races, career)
engine/          Godot 4.7 project (rendering, input, audio, menus)
tests/           headless acceptance tests (no external deps)
```

## Status

See `docs/ROADMAP.md` — M0/M0.5/M1/M2a complete (22 acceptance tests green).
Next up: visual polish pass, more cars/tracks, force feedback tuning.

## License

MIT for all code. Assets CC0/CC-BY/original. See `LICENSE`.
