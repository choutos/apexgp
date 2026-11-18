# ApexGP

*(working title — final name TBD)*

An open-source, clean-room racing simulator built in the spirit of late-90s
console GT-style games: an approachable sim handling model, license tests, a
used-car economy and a deep tuning shop — with **100% original code and
assets**. No content, code, data, or design documents from any commercial
title are referenced, derived from, or shipped with this project.

## Principles

1. **Clean-room, always.** Fictional marques, original circuits, original name.
   Public-domain vehicle-dynamics literature is the only reference material.
2. **Sim-first, accessible by default.** Honest physics (Pacejka-family tire
   model, weight transfer, drivetrain) with GT-era assists (ABS / TCS / ASM)
   on by default and fully configurable.
3. **Data-driven content.** Cars and tracks are plain data files — moddable
   and community-extensible from day one.
4. **Vertical slice discipline.** Driving feel outranks content volume until
   the feel is right.

## Stack

- **Simulation core**: dependency-free C# (`net8.0`) — pure math, no engine
  coupling, fully testable headless (`src/ApexGP.Core`).
- **Engine shell**: Godot 4 (.NET) for rendering, input, audio and packaging
  (planned, M0.5).

## Build & test

```sh
dotnet build
dotnet run --project tests/ApexGP.Tests
```

Exit code 0 = all physics acceptance tests pass.

## Layout

```
docs/            design, roadmap, physics model spec
src/ApexGP.Core  simulation core (rigid body, tires, suspension, drivetrain)
tests/           headless acceptance tests (plain .NET, no external deps)
```

## Status

M0 (sim core) — in progress. See `docs/ROADMAP.md`.
