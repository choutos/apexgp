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

## Current state (v0.4 "PS1 Look & Feel")

- **Simulation core** (`src/ApexGP.Core`, pure C#): 6-DOF chassis, Pacejka tire
  model with load sensitivity and combined-slip ellipse, independent
  suspension (spring/damper/ARB/bump stops), torque-curve engine, viscous
  clutch with launch behaviour, 5-speed auto gearbox, ABS + progressive TCS.
- **Track pipeline**: Catmull-Rom circuit builder with racing line and a
  braking-aware speed profile; tarmac/kerb/grass grip model; lap timing with
  cut detection. Circuits: *Sunrise Loop* (1.38 km), *Dolphin Ring*, *Kanyu
  Pass* (technical hillclimb-style with hairpin, esses and chicane).
- **Racing**: grid starts, live positions, sphere-based car-to-car contacts,
  invisible walls with forgiving bounce (no infield cuts), results podium.
- **AI**: racing-line followers with per-driver pace; rivals use varied cars.
- **Career**: credits, garage, dealership (7 original models), tuning shop
  (7 parts with real physics effects), licence school (club on Sunrise Loop,
  national on Dolphin Ring), 6 events with entry requirements and payouts.
  Profile persists to JSON.
- **Godot 4.7 shell** (`engine/`): 480×270 nearest-upscaled PS1-style render,
  detailed low-poly cars (brake lights, spoilers, hubs), track furniture
  (guard rails, trees, start gantry), chase camera, HUD
  (speed/gear/rpm/position/lap), procedural engine audio, gamepad rumble on
  tire slip, keyboard + gamepad input, Esc pause, and replay playback
  (press **R** on the results screen).

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

See `docs/ROADMAP.md` — 30 acceptance tests green (sim, tracks, races,
contacts, career economy, replay round-trip).
Next up: damage model (optional), split-screen, more circuits.

## Releases

Every `vX.Y.Z` tag triggers `.github/workflows/release.yml`, which exports
standalone **Linux x86_64** and **Windows x86_64** builds and attaches them
to the GitHub release (smoke-tested in CI before upload). SHA256 checksums
are included in each release.

Running an export locally (requires Godot 4.7 .NET + export templates):

```sh
godot --headless --path engine --export-release "Linux" ../build/ApexGP.x86_64
```

## License

MIT for all code. Assets CC0/CC-BY/original. See `LICENSE`.
