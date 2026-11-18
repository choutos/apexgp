# ApexGP — Design

## Vision

Capture the *spirit* of the late-90s console GT formula in an open, clean-room
implementation: a driving-sim where the car feels like a car, progression is
earned through license tests and race series, and every part of the car can be
tuned. The original game proved this formula works with modest hardware and
enormous care; we take the care and leave the proprietary bits behind.

## Clean-room rules (hard constraints)

- No reference to any commercial game's code, data, asset formats, or internal
  design documents (including community reverse-engineering projects — they
  are not used as a spec, per project decision).
- Fictional marques and model names only. Car designs are original works.
- Original circuit designs only — no 1:1 recreations of real or licensed
  circuits.
- Original project name; the words "Gran Turismo" and "GT" as a product mark
  are off-limits.
- All assets are CC0 / CC-BY / original. All code MIT. Anything less would
  poison the project.

## Feel target

The reference *experience* (not code): a car that is honest about grip but
forgiving by default. Weight transfer you can feel, brakes that lock if you
disable ABS, a clutch that matters. Assists ON for casual play, OFF for the
sim crowd. Physics rate decoupled from render rate (240 Hz+ sub-stepping).

## Content model

Everything is data: cars (torque curve, gearing, mass, geometry, tire
compound), tracks (spline + cross-sections), events (requirements, prizes).
The simulation consumes data; editors produce data. Community content is a
first-class citizen, not an afterthought.

## Engine choice

Godot 4 (.NET). The sim core is deliberately engine-agnostic pure C# so it
can be unit-tested headless and (later) swapped into any host. Godot provides
rendering, input, audio, scene management and export. No built-in vehicle
physics are used — they are arcade-grade.
