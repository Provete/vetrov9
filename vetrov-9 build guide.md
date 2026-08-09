# Vetrov-9 — Project Build Guide

## How to read this

Every phase below has the same four parts: **Goal** (why this phase exists), **What to build** (a concrete ordered checklist), **Test checkpoint** (what a test for this phase should actually assert), **Done when** (an unambiguous finish line). Contracts show type/function *signatures* only — the logic inside them is yours to write.

## The big picture

Twelve phases, strictly ordered — each one deliberately gives the next something solid to stand on. Pure/no-dependency work comes first, real-world grounding happens early, "the world" (Simulation) gets built only after "the brain" (Core) already exists and is tested.

| # | Phase | Project | In one line |
|---|---|---|---|
| 0 | Solution skeleton | — | Scaffold the 5 projects, prove they reference each other correctly |
| 1 | Domain types | Core (F#) | Model readings so invalid ones can't compile; real calibration constants |
| 2 | VDF protocol codec | Core (F#) | Bytes ↔ typed readings, in two stages |
| 3 | Detection algorithms | Core (F#) | The "brain" — event detection + origin classification, pure functions |
| 4 | Event bus | Simulation (C#) | Publish/subscribe, decoupled |
| 5 | Ring buffer & register | Simulation (C#) | The two ingestion-side storage primitives |
| 6 | Sim clock & DES scheduler | Simulation (C#) | Drives *when* anything happens |
| 7 | Baseline sensor simulators | Simulation (C#) | The "normal" world — noise, weather, generator |
| 8 | The Sigma anomaly | Simulation (C#) | The thing that isn't normal |
| 9 | Ingestion pipeline | Simulation (glue) | Drain → encode → wire → decode → publish |
| 10 | Console host | Console (C#) | Wire it all together, watch it run |
| 11 | Close the loop | Console (C#) | Detection algorithms subscribe live |

## Before Phase 0: the one cross-cutting decision

Never call `DateTime.Now` (or any real-time API) directly from Simulation code. Define this and inject it into every scheduler, every sensor simulator, everywhere time matters:

```csharp
public interface ISimClock
{
    DateTime Now { get; }
    void AdvanceTo(DateTime t);
}
```

If you skip this and retrofit it later, you'll be rewriting half the project. With it, tests fast-forward through simulated days in milliseconds, deterministically, every run.

---

## Phase 0 — Solution skeleton

**Goal:** a solution that builds, with the dependency graph enforced by project references, before any real logic exists.

**What to build:**

```
dotnet new sln -n Vetrov9

dotnet new classlib -lang F# -n Vetrov9.Core          -o src/Vetrov9.Core
dotnet new classlib -lang C# -n Vetrov9.Simulation    -o src/Vetrov9.Simulation
dotnet new console  -lang C# -n Vetrov9.Console       -o src/Vetrov9.Console
dotnet new xunit    -lang F# -n Vetrov9.Core.Tests    -o tests/Vetrov9.Core.Tests
dotnet new xunit    -lang C# -n Vetrov9.Simulation.Tests -o tests/Vetrov9.Simulation.Tests

dotnet sln add src/Vetrov9.Core src/Vetrov9.Simulation src/Vetrov9.Console tests/Vetrov9.Core.Tests tests/Vetrov9.Simulation.Tests

dotnet add src/Vetrov9.Simulation reference src/Vetrov9.Core
dotnet add src/Vetrov9.Console reference src/Vetrov9.Core
dotnet add src/Vetrov9.Console reference src/Vetrov9.Simulation
dotnet add tests/Vetrov9.Core.Tests reference src/Vetrov9.Core
dotnet add tests/Vetrov9.Simulation.Tests reference src/Vetrov9.Simulation
```

Nothing references `Vetrov9.Console` — that's the whole point of the layering. Put a one-line `Console.WriteLine("Vetrov-9 online")` in `Program.cs` just to prove the exe runs.

**Test checkpoint:** none yet — there's no logic. Just confirm both test projects run with zero tests in them (proves the test runner and F#/C# interop both actually work before you build anything on top).

**Done when:** `dotnet build` succeeds for the whole solution, `dotnet run --project src/Vetrov9.Console` prints your line, `dotnet test` runs (green, zero tests).

---

## Phase 1 — Domain types (`Core`, F#)

**Goal:** model every sensor reading so that an invalid one — wrong physical unit, or a reading "from" a node that doesn't have that sensor — is a compile error, not a bug you find later. Also settle the calibration constants now, using real instrument references instead of arbitrary numbers.

**What to build, in order:**

1. **`Axis`** — the three components shared by both 3-axis sensors:
   ```fsharp
   type Axis = Z | N | E
   ```

2. **Restricted per-category node types** — instead of one flat `NodeId` shared by every reading kind, give each kind only the nodes that physically have that sensor. This is what makes `Seismic(Mag1, ...)` uncompilable:
   ```fsharp
   type RingANode = AA1 | AA2 | AA3 | AA4          // seismic + infrasound
   type RingBNode = AB1 | AB2 | AB3 | AB4          // seismic only, no infrasound
   type SeismicNode = RingA of RingANode | RingB of RingBNode
   type MagnetometerNode = Mag1 | Mag2
   type ThermistorNode = Bh1 | Bh2
   ```
   Hydrophone, wind, air temp, pressure, and generator telemetry each only ever exist on exactly one node (BH-1 or the Domo) — give those cases no node field at all, rather than a field that can only hold one value.

3. **`SensorReading`** — one case per sensor kind, each carrying only its own restricted node type and its own physical unit:
   ```fsharp
   type SensorReading =
       | Seismic    of node: SeismicNode * axis: Axis * velocityNmS: float * t: DateTime
       | Infrasound of node: RingANode * pressureMPa: float * t: DateTime
       | Magnetic   of node: MagnetometerNode * axis: Axis * fieldDeltaNt: float * t: DateTime
       | Thermal    of node: ThermistorNode * depthM: float * tempC: float * t: DateTime
       | Hydrophone of pressureUPa: float * t: DateTime
       | Wind       of speedMs: float * dirDeg: float * t: DateTime
       | AirTemp    of tempC: float * t: DateTime
       | Pressure   of hPa: float * t: DateTime
       | Telemetry  of generatorRpm: float * powerKw: float * vibrationMmS: float * t: DateTime
   ```
   Note `fieldDeltaNt`, not `fieldNt` — real magnetometers of this kind read as a *deviation* from a nulled baseline, not the absolute ~61,000 nT field directly. Naming it honestly here saves you from a wrong assumption three phases from now.

4. **Calibration** — one pure function per sensor kind, raw ADC count (or, for wind, raw pulse count) in, physical unit out. These constants are grounded in real instrument classes, not picked arbitrarily — see the scenario doc's calibration table for the reasoning behind each number:
   ```fsharp
   module Calibration =
       let toSeismicVelocity    (raw: int) : float = float raw * 1.0     // nm/s
       let toInfrasoundPressure (raw: int) : float = float raw * 0.006   // mPa
       let toMagneticFieldDelta (raw: int) : float = float raw * 0.0005  // nT, deviation from baseline
       let toThermalTemp        (raw: int) : float = float raw * 0.005   // °C
       let toHydrophonePressure (raw: int) : float = float raw * 42.0    // µPa
       let toWindSpeed    (pulseCount: int) : float = float pulseCount * 0.078  // m/s — a PULSE count, not an amplitude
       let toBarometricPressure (raw: int) : float = float raw * 0.01    // hPa (resolution — real accuracy is coarser, ~±0.3 hPa)
   ```
   Generator RPM/power/vibration don't get research-grade constants — they're a reference channel, not a scientific measurement — a simple linear scaling of your choice is fine there.

5. A small lookup bridging a typed node back to its physical (x, y) coordinates from the scenario doc's topology table — you'll need this for TDOA math in Phase 3, might as well settle it now:
   ```fsharp
   val coordinatesOf : SeismicNode -> float * float
   ```

**Test checkpoint:**
- Compile-time: deliberately try to write `Seismic(Mag1, Z, 0.0, DateTime.UtcNow)` somewhere and confirm it fails to compile. That failure *is* the test for the type design.
- Runtime: for each calibration function, one test with a hand-computed input/output pair (e.g. `toSeismicVelocity 1000` should be `1000.0`, `toMagneticFieldDelta 2000` should be `1.0`). These are trivial multiplications, but they're your safety net for when you tune the constants later.

**Done when:** the project compiles, the illegal-construction check fails to compile as expected, and calibration tests pass.

---

## Phase 2 — VDF protocol codec (`Core`, F#)

**Goal:** convert between raw bytes on the wire and the typed `SensorReading` values from Phase 1 — in **two separate stages**, because they can fail for different reasons and shouldn't be tangled together.

- **Stage A (structural):** is this a well-formed frame at all? Sync word present, checksum correct, not truncated. Doesn't know or care what a "seismic node" is.
- **Stage B (semantic):** given a structurally-valid frame, does its node byte actually belong to the node family its channel byte implies? This is where a byte-level `NodeId`/`ChannelId` gets promoted into the strict Phase 1 types — and where calibration gets applied.

**What to build:**

1. **Address tables** — one byte per physical node, one byte (or range) per channel:

   | Node | Byte | | Node | Byte |
      |---|---|---|---|---|
   | DM0 | `0x00` | | BH1 | `0x09` |
   | AA1 | `0x01` | | BH2 | `0x0A` |
   | AA2 | `0x02` | | MAG1 | `0x0B` |
   | AA3 | `0x03` | | MAG2 | `0x0C` |
   | AA4 | `0x04` | | | |
   | AB1 | `0x05` | | | |
   | AB2 | `0x06` | | | |
   | AB3 | `0x07` | | | |
   | AB4 | `0x08` | | | |

   | Channel | Byte(s) | Valid node byte(s) |
      |---|---|---|
   | SeismicZ / N / E | `0x01` / `0x02` / `0x03` | `0x01`–`0x08` |
   | Infrasound | `0x10` | `0x01`–`0x04` only (ring A) |
   | MagZ / N / E | `0x20` / `0x21` / `0x22` | `0x0B`–`0x0C` |
   | Thermal (one byte per depth index) | `0x30`–`0x4F` | `0x09`–`0x0A` |
   | Hydrophone | `0x50` | `0x09` only |
   | WindPulses | `0x51` | `0x00` only |
   | AirTemp | `0x52` | `0x00` only |
   | Pressure | `0x53` | `0x00` only |
   | GeneratorRpm | `0x60` | `0x00` only |
   | GeneratorPowerKw | `0x61` | `0x00` only |
   | GeneratorVibration | `0x62` | `0x00` only |

   The "valid node bytes" column is exactly Stage B's validation logic. A thermal string's specific depth-index-to-meters mapping (e.g. channel `0x30` = 100 m at BH-1 but a different depth at BH-2, since the two strings space their thermistors differently) is a small lookup table, not something encoded in the byte itself.

2. **The raw structural type and errors:**
   ```fsharp
   type RawFrame = { NodeByte: byte; ChannelByte: byte; Timestamp: DateTime; RawSamples: int[] }

   type ParseError =
       | BadSyncWord
       | ChecksumMismatch
       | Truncated
       | UnknownNode of byte
       | UnknownChannel of byte
       | NodeChannelMismatch of node: byte * channel: byte
   ```

3. **Stage A — structural encode/decode**, sync word `0xAA55`, big-endian throughout:
   ```fsharp
   val encodeFrame : RawFrame -> byte[]
   val decodeFrame : ReadOnlySpan<byte> -> Result<RawFrame, ParseError>
   ```

4. **Stage B — semantic mapping**, using the table above plus Phase 1's calibration functions:
   ```fsharp
   val toSensorReading : RawFrame -> Result<SensorReading, ParseError>
   ```

**Test checkpoint:**
- Stage A round-trip: `decodeFrame (encodeFrame frame) = Ok frame`, for a hand-built frame. This is the single most valuable test in the codec.
- Stage A failure cases: wrong sync word, truncated mid-header, truncated mid-payload, corrupted checksum → each should produce its specific `ParseError`, not a crash.
- Stage B success: for one valid `RawFrame` per sensor kind, `toSensorReading` produces the correctly-typed, correctly-calibrated `SensorReading`.
- Stage B failure: a frame with node byte `0x0B` (MAG1) and channel byte `0x01` (SeismicZ) → `NodeChannelMismatch`.

**Done when:** all of the above pass, and — good gut check — feed `encodeFrame` output for every sensor kind through `decodeFrame` then `toSensorReading` and confirm you get back exactly what you put in.

---

## Phase 3 — Detection & classification algorithms (`Core`, F#)

**Goal:** the "brain," built and fully tested against hand-crafted arrays before any simulator exists.

**What to build:**

1. **STA/LTA event detector.** This is a standard seismology technique: at each sample, maintain a rolling short-window average of signal amplitude (or energy) and a rolling long-window average of the same. Divide short by long. When that ratio crosses above a threshold (commonly 3–5), the signal has picked up sharply relative to its recent background — that's a trigger. Output the sample indices where triggers occur.
   ```fsharp
   val detectStaLta :
       samples: float[] -> shortWindow: int -> longWindow: int -> threshold: float -> int list
   ```

2. **Origin classifier.** Given a detected seismic event (its node, its onset time) and the infrasound readings from that same node around that time: using the ~300 m/s air-propagation constant from the scenario doc, compute the arrival window a surface-coupled source at the same location would produce in infrasound. If a real infrasound reading above its own noise floor falls inside that window → `SurfaceCoupled`. If nothing does → `Subsurface`.

   One real edge case worth deciding deliberately rather than discovering by accident: **ring B nodes (`AB1`–`AB4`) have no infrasound sensor at all.** A seismic event detected only on a ring B node can never be positively confirmed `SurfaceCoupled` by this test — there's no infrasound channel there to check. Add a third case rather than forcing a guess:
   ```fsharp
   type Origin = SurfaceCoupled | Subsurface | Unknown
   val classifyOrigin :
       seismicEvent: SeismicEvent -> infrasoundWindow: SensorReading list -> Origin
   ```

**Test checkpoint:**
- STA/LTA: a flat-noise array with one injected spike → trigger index matches the spike's position; pure flat noise → zero triggers.
- Classifier, ring A node: matching infrasound arrival in-window → `SurfaceCoupled`; no infrasound reading in-window → `Subsurface`.
- Classifier, ring B node: no infrasound data possible at all → confirm it resolves to whatever you decided (`Unknown`, most likely) rather than silently defaulting to `Subsurface` by accident.

**Done when:** all three test groups pass using only hand-built fixtures — no simulator, no scheduler, nothing but arrays and lists you typed yourself.

---

## Phase 4 — Event bus (`Simulation`, C#)

**Goal:** let a producer (eventually, the ingestion pipeline) publish a `SensorReading` without knowing who — if anyone — is listening.

```csharp
public interface IEventBus
{
    void Publish<T>(T message);
    IDisposable Subscribe<T>(Action<T> handler);
}
```

F# types cross this boundary with no friction — `SensorReading` is a normal .NET type under the hood, so `Publish<SensorReading>` works exactly like publishing any C# type.

**Test checkpoint:** two handlers subscribed to two different message types, publish one of each, confirm only the matching handler fires; confirm disposing a subscription actually stops future delivery.

**Done when:** those pass. Nothing publishes anything real yet — that starts in Phase 9.

---

## Phase 5 — Ring buffer & register primitives (`Simulation`, C#)

**Goal:** the two ingestion-side storage shapes, matching each sensor's actual rate — a single-slot register would silently drop samples from a 100 Hz seismometer, and a ring buffer is pointless overhead for something that updates once every 15 minutes.

**What to build:**

```csharp
public interface IRingBuffer<T>
{
    void Write(T value);
    IReadOnlyList<T> DrainAll();
}

public interface IRegister<T>
{
    void Write(T value);
    bool HasNewData { get; }
    T Read(); // clears HasNewData
}
```

**Which sensor uses which:**

| Ring buffer (high rate) | Register (low rate) |
|---|---|
| Seismic — all 8 seismic nodes | Magnetic — 2 nodes |
| Infrasound — 4 ring A nodes | Thermal — 2 nodes, one register per depth point |
| Hydrophone — BH-1 | Wind, AirTemp, Pressure — Domo |
| | Telemetry — RPM, power, vibration each get their own register, Domo |

**Test checkpoint:**
- Ring buffer: write past capacity → oldest entries dropped, remaining order preserved; `DrainAll()` then write again starts clean.
- Register: write → `HasNewData` true; read → correct value returned, flag clears; reading twice without an intervening write doesn't throw or falsely re-flag.

**Done when:** both pass for a generic `T`, independent of any real sensor.

---

## Phase 6 — Simulated clock & DES scheduler (`Simulation`, C#)

**Goal:** drive *when* things happen without wasting cycles polling sensors that have nothing new to report — a uniform tick-every-object loop is a bad fit here, since your sample rates span from 100 Hz down to once per 15 minutes. Instead: a priority queue of "next event," jump straight to the next one due.

**What to build:**

```csharp
public interface IScheduledProducer
{
    DateTime NextFireTime { get; }
    void Fire(ISimClock clock);
}
```

Use `PriorityQueue<IScheduledProducer, DateTime>` (built into .NET) as the queue. Prove it with a **dummy producer** — fires on a fixed interval, records when it was called — before plugging in anything real.

The uplink window (Phase 9's trigger) is just another `IScheduledProducer`, firing on a much slower cadence than any sensor. Nothing to build for it yet — just know it slots into this same scheduler later, not a separate mechanism.

**Test checkpoint:** register a few dummy producers with different fixed intervals, run for a fixed simulated duration, confirm each fired the expected number of times at the expected timestamps, and that firings from different producers interleave in correct overall time order regardless of registration order.

**Done when:** that passes, using only dummy producers — no real sensor logic yet.

---

## Phase 7 — Baseline sensor simulators (`Simulation`, C#)

**Goal:** everything that counts as "normal" — the noise floor, the weather, the equipment humming in the background — implemented one `IScheduledProducer` per sensor, in an order that respects a real dependency: the seismometer's noise model needs the generator's current vibration state and the current wind speed to exist *before* it can compute its own coupling, so those come first.

**Build order and what each one does:**

1. **Thermistor** (register-based). Near-constant: the slow geothermal gradient baseline from the scenario doc (~0.025 °C/m) plus a tiny random walk (±0.005 °C) for instrumental noise. Nothing else moving.

2. **Met station** — wind, air temp, pressure (register-based). Needs to look weather-like, not like uniform random noise — a mean-reverting random walk (Ornstein–Uhlenbeck-style: values drift, but always pulled back toward a long-run average rather than wandering forever) is a reasonable technique here, with occasional longer excursions to represent a passing weather system.

3. **Magnetometer** (register-based). A smooth ~24-hour diurnal component (small amplitude, tens of nT) plus rare, larger, longer-lasting excursions representing geomagnetic storms, plus the ~40 pT instrument noise floor from the calibration doc. No relation to the anomaly — pure environmental noise.

4. **Generator telemetry** (register-based, 3 sub-signals). Mostly a stable nominal RPM/power/vibration state with small fluctuation. Read by the seismic simulator below.

   **Important:** the seismic simulator's coupling to the generator is **not** done through the event bus or the wire protocol — it's a direct, in-process reference (e.g. the seismic simulator constructor takes an `IGeneratorState` it reads from live). Vibration physically travels through the ground; it isn't "data" being transmitted between components. The `Telemetry` reading that later gets published over the wire is a separate thing — the generator's own instrumented output, arrived at through its own simulated measurement chain, that happens to correlate with what the seismometer feels.

5. **Seismometer** (ring buffer). Instrument noise floor (~1–2 counts, matching the ~1–2 nm/s real noise floor) plus:
    - **Cryoseism impulses** — short, broadband, impulsive events, several dozen per day, more frequent at night (rapid thermal contraction).
    - **Generator coupling** — a small signal at the generator's harmonic (50/60 Hz), scaled down with distance from the Domo, read from the live generator state (point 4).
    - **Wind coupling** — small additional noise scaled by current wind speed (from the met station's live state).

6. **Infrasound** (ring buffer). Low baseline noise, but noise floor rises sharply with wind speed (from the met station), saturating above ~15 m/s per the scenario doc.

7. **Hydrophone** (ring buffer). Near-total silence — very low amplitude random noise only. Nothing else contributes; that's the point.

**Test checkpoint (statistical, not exact-value):** run each sensor for N simulated samples; confirm the distribution stays within its documented noise band; confirm saturation clips correctly at documented limits; for the seismometer specifically, confirm cryoseism-rate over a long run roughly matches "several dozen per day."

**Done when:** all seven pass their statistical checks, and — a good sanity pass — temporarily crank the met station's wind way up and confirm the infrasound noise floor visibly rises and the seismometer's wind-coupling term visibly increases, proving the cross-sensor coupling is actually wired, not just present in name.

---

## Phase 8 — The Sigma anomaly generator (`Simulation`, C#)

**Goal:** the one thing that isn't normal, built last among generators since it only makes sense once "normal" already exists to be distinguished from.

**What to build:**

Give Sigma a single simulated 2D position that evolves over the course of a flare-up (a slow, mildly biased random walk works fine) — and derive every cross-channel signature from that *same* shared position, rather than generating each channel's signature independently:

- **Seismic events**: fire on the ~1.6–2.4 s jittered interval from the scenario doc; amplitude at each node decays with that node's distance from the current Sigma position.
- **Magnetic**: a small dipole perturbation, strongest at whichever magnetometer node is currently closest to the Sigma position, falling off quickly with distance.
- **Thermal**: a brief transient at whichever thermistor node/depth is currently closest to the Sigma position.
- **Infrasound**: deliberately absent — the one non-negotiable invariant. No Sigma-sourced seismic event should ever produce a correlated infrasound arrival, since that's exactly what Phase 3's classifier depends on to ever detect anything as `Subsurface`.

**Test checkpoint:**
- Inter-event interval falls in the 1.6–2.4 s range across many generated events.
- The non-negotiable one: assert directly that no Sigma seismic event has a matching infrasound arrival, over a long generated run.
- Amplitude at a given node correlates with that node's distance from the Sigma position at the time of the event (closer → louder).

**Done when:** all three pass, plus a manual look at a run's magnetic/thermal transients confirms they cluster near wherever the seismic events were happening at the same time — proof the shared-position mechanism is actually producing correlated signatures, not three independent random processes that happen to share a name.

---

## Phase 9 — Ingestion pipeline (glue: `Simulation` calling into `Core`)

**Goal:** connect everything built so far into one working pipeline.

**What to build:**

The uplink-window `IScheduledProducer` from Phase 6 fires, and when it does: for every node, drain its ring buffers (`DrainAll()`) and read its registers (`Read()` where `HasNewData`), call Core's `encodeFrame` (Phase 2) per reading, concatenate into one byte stream — this is your simulated satellite downlink. A parser routine then walks that stream, calling `decodeFrame` (Stage A) then `toSensorReading` (Stage B) per frame, and publishes each resulting `SensorReading` onto the Phase 4 bus.

One easy mistake here: remember wind's raw value is a **pulse count**, not an ADC amplitude like everything else — the met station simulator needs to be counting pulses over the sample interval, not sampling a continuous value, or Phase 1's `toWindSpeed` calibration will be operating on the wrong kind of number entirely.

**Test checkpoint:** end-to-end — run the scheduler for a fixed simulated period (including several baseline sensors and at least one Sigma event), trigger an uplink, assert that what comes out the far end of the bus matches (within calibration rounding) what went into the buffers and registers at the start.

**Done when:** that passes. This is the test proving every earlier phase actually fits together, not just compiles in isolation.

---

## Phase 10 — Console host (`Console`, C#)

**Goal:** wire everything into a runnable program.

**What to build:** the composition root — construct the clock, the bus, every sensor simulator (wiring the generator/met-state dependencies from Phase 7 correctly), the scheduler, register every producer, run the loop. Subscribe one trivial handler to the bus that just `Console.WriteLine`s every reading — proving the wiring, not building UI.

**Done when:** running the exe produces a live (simulated-time) stream of readings in the terminal, sourced entirely through the pipeline built in Phases 1–9, with no shortcuts.

---

## Phase 11 — Close the loop

**Goal:** watch detection actually happen.

**What to build:** subscribe Phase 3's `detectStaLta` and `classifyOrigin` to the bus as consumers, so classification runs live as the simulation runs instead of only against hand-built fixtures.

**Done when:** you can watch a Sigma event get generated (Phase 8), travel through the wire (Phase 9), and come out the other end flagged `Subsurface` — live, in your terminal, with nothing hand-fed.
