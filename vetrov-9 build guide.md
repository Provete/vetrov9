# Vetrov-9 — Project Build Guide

This is a dependency-ordered build sequence, not code. Each phase lists what to build, which project/language it belongs in, the *contract* (type/function signatures only — the logic is yours), and what a test for that phase should actually assert. Do them in order; each one deliberately gives the next one something solid to stand on.

## Before Phase 0: the one cross-cutting decision

Never call `DateTime.Now` (or any real-time API) directly from Simulation code. Define an `ISimClock` abstraction from the very start and inject it everywhere — the scheduler, every sensor simulator, the uplink-window trigger. If you skip this and retrofit it later, you'll be rewriting half the project. With it, your tests can fast-forward through days of simulated time in milliseconds and get identical results every run.

```csharp
public interface ISimClock
{
    DateTime Now { get; }
    void AdvanceTo(DateTime t);
}
```

## Language map

| Project | Language | Why |
|---|---|---|
| `Vetrov9.Core` | F# | Pure data types + pure algorithms (protocol codec, detection/classification). No mutable state, no I/O — F#'s strengths, no downsides. |
| `Vetrov9.Simulation` | C# | Mutable state, timers, ring buffers, DES loop — imperative-shell territory. |
| `Vetrov9.Console` | C# | Composition root, rendering, entry point. |
| `Vetrov9.Core.Tests` | F# (or C#, your call) | Testing a pure F# library reads naturally in F# too, but xUnit works fine from either. |
| `Vetrov9.Simulation.Tests` | C# | Matches the project under test. |

---

## Phase 0 — Solution skeleton

Create the solution and all five projects above, wire up project references exactly per the dependency diagram from earlier (Console → Simulation → Core; nothing points back). Get a trivial `Program.cs` printing something to prove F#↔C# interop compiles and the whole thing runs. No logic yet.

**Done when:** `dotnet build` and `dotnet run` both work, and `Core.Tests` / `Simulation.Tests` both run (even with zero real tests in them).

---

## Phase 1 — Domain types (`Core`, F#)

Define, as plain data with no behavior:
- `NodeId` — one case per station from your topology table (DM0, AA1..4, AB1..4, BH1, BH2, MAG1, MAG2)
- `ChannelId` — one case per sensor channel/axis (SeismicZ/N/E, Infrasound, MagZ/N/E, Thermal of depth, Hydrophone, wind/temp/pressure)
- `SensorReading` — the DU you already sketched, keyed by these two types instead of bare strings
- A calibration table mapping `ChannelId -> countsToUnit: int -> float`, since the wire format carries raw counts, not physical units

```fsharp
val toPhysicalUnits : ChannelId -> rawCount: int -> float
```

**Done when:** compiles, and you can construct one instance of each reading case by hand in a REPL or a throwaway test. Nothing to assert yet beyond "it exists."

---

## Phase 2 — VDF protocol codec (`Core`, F#)

This is your first real logic, and deliberately still 100% pure — no clock, no state.

```fsharp
type ParseError =
    | BadSyncWord
    | ChecksumMismatch
    | Truncated
    | UnknownNode of byte
    | UnknownChannel of byte

val encode : VdfFrame -> byte[]
val decode : ReadOnlySpan<byte> -> Result<VdfFrame, ParseError>
```

**Test checkpoint:**
- Round-trip: for a hand-built frame, `decode (encode frame) = Ok frame`. This is your single most valuable test in the whole project — if this holds, the wire format is sound.
- Feed deliberately broken input for each `ParseError` case: wrong sync word, truncated buffer mid-header, truncated buffer mid-payload, flipped byte in the checksum region.
- Edge cases: zero samples, the max sample count your 1-byte field allows (255), a frame containing negative sample values.
- If you want more coverage for less effort: this is a great fit for **FsCheck** (property-based testing) — generate random valid frames and assert the round-trip property holds for all of them, instead of hand-writing a dozen cases.

---

## Phase 3 — Detection & classification algorithms (`Core`, F#)

Build these *before* the simulator exists, using hand-crafted arrays as fixtures — you don't need live data to know if the brain works.

```fsharp
val detectStaLta :
    samples: float[] -> shortWindow: int -> longWindow: int -> threshold: float -> int list // trigger indices

val classifyOrigin :
    seismicEvent: SeismicEvent -> infrasoundWindow: Reading list -> Origin // SurfaceCoupled | Subsurface
```

**Test checkpoint:**
- STA/LTA: feed a flat-noise array with one injected spike, assert the trigger index matches where you put the spike; feed pure flat noise, assert zero triggers.
- Classifier: feed a seismic event with a matching infrasound arrival inside the expected travel-time window → `SurfaceCoupled`; same seismic event with no infrasound reading in that window → `Subsurface`. This second case is the one your whole Sigma detection depends on later, so it's worth a few variations (infrasound arrives *just* outside the window, infrasound list is empty entirely, etc.).

---

## Phase 4 — Event bus (`Simulation`, C#)

Small and standalone — build it now since everything from Phase 9 onward needs it, but it has no dependency on anything else you've built.

```csharp
public interface IEventBus
{
    void Publish<T>(T message);
    IDisposable Subscribe<T>(Action<T> handler);
}
```

**Test checkpoint:** subscribe two different handlers to two different message types, publish one of each, assert only the matching handler fired and the other didn't; assert disposing a subscription actually stops it from receiving further messages.

---

## Phase 5 — Ring buffer & register primitives (`Simulation`, C#)

Your two ingestion-side data structures, independent of any sensor logic.

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

**Test checkpoint:**
- Ring buffer: write more items than capacity, assert the oldest were dropped and the remaining ones are in the correct order; `DrainAll()` then `Write` again should start clean.
- Register: write once, assert `HasNewData` is true; read, assert it returns the written value and `HasNewData` flips to false; reading twice in a row without an intervening write should not throw or falsely re-flag.

---

## Phase 6 — Simulated clock & DES scheduler (`Simulation`, C#)

Implement the `ISimClock` from the "before Phase 0" note, plus the event-driven loop.

```csharp
public interface IScheduledProducer
{
    DateTime NextFireTime { get; }
    void Fire(ISimClock clock);
}
```

Use `PriorityQueue<IScheduledProducer, DateTime>` (built into .NET) as the queue. Prove the loop with a **dummy producer** that just fires on a fixed interval and records the times it was called — don't plug in a real sensor yet.

**Test checkpoint:** register a few dummy producers with different fixed intervals, run the loop for a fixed simulated duration, assert each fired the expected number of times at the expected timestamps, and that firings across different producers came out in correct overall time order regardless of registration order.

---

## Phase 7 — Baseline sensor simulators (`Simulation`, C#)

Implement `IScheduledProducer` for each real sensor, **simplest first**: thermistor → met station → magnetometer → seismometer (baseline noise only — cryoseisms, generator harmonic, wind coupling per your scenario doc) → infrasound → hydrophone. Each one computes a raw count and writes into its register (low-rate) or ring buffer (high-rate).

**Test checkpoint:** these are statistical, not exact-value, tests. Run a sensor for N simulated samples and assert the *distribution* stays within the documented noise floor/band from the scenario doc; assert values clip correctly at the documented saturation limits; for the seismometer specifically, assert cryoseism-type impulsive events occur at roughly the expected daily rate over a long simulated run.

---

## Phase 8 — The Sigma anomaly generator (`Simulation`, C#)

Deliberately last among the generators — it only makes sense once "normal" already exists to be distinguished from.

**Test checkpoint:** over many generated events, assert the inter-event interval falls in your 1.6–2.4s range; the one non-negotiable invariant to test explicitly: **no Sigma-sourced seismic event should ever produce a correlated infrasound arrival.** That invariant is what Phase 3's classifier depends on to ever detect anything.

---

## Phase 9 — Ingestion pipeline (glue: `Simulation` calling into `Core`)

The uplink-window event (itself just another `IScheduledProducer`, fired on a much slower cadence) drains every node's buffer/register, calls Core's `encode` per frame, concatenates into one byte stream. A parser routine walks that stream, calls Core's `decode`, converts counts→units via the Phase 1 calibration table, and publishes the resulting `SensorReading` onto the Phase 4 bus.

**Test checkpoint:** end-to-end integration test — run the scheduler for a fixed simulated period, trigger an uplink, assert that what comes out the far end of the bus matches (within calibration rounding) what went into the buffers at the start. This is the test that proves every earlier phase actually fits together.

---

## Phase 10 — Console host (`Console`, C#)

Composition root: construct the clock, the bus, every sensor, the scheduler, register everything, run the loop. Rendering can start as literally `Console.WriteLine` on every published reading — the point of this phase is proving the wiring, not building UI.

**Done when:** running the exe produces a live (simulated-time) stream of readings in the terminal, sourced entirely through the pipeline you built in Phases 1–9.

---

## Phase 11 — Close the loop

Subscribe Phase 3's detection/classification functions to the bus as consumers, so classification happens live as the simulation runs instead of only in isolated unit tests. This is the phase where you'll actually *see* a Sigma event get flagged as subsurface while it's happening.
