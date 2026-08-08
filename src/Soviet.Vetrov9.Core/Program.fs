namespace Soviet.Vetrov9.Core

open System

type RingANode = AA1 | AA2 | AA3 | AA4
type RingBNode = AB1 | AB2 | AB3 | AB4
type SeismicNode = RingA of RingANode | RingB of RingBNode
type MagnetometerNode = Mag1 | Mag2
type ThermistorNode = Bh1 | Bh2
type Axis = Z | N | E

type SensorReading =
    | Seismic of node: SeismicNode
        * axis: Axis
        * speedNmPerS: float // Speed in nanometers per second
        * t: DateTime
    | Infrasound of node: RingANode
        * pressureMPa: float
        * t: DateTime
    | Magnetic of node: MagnetometerNode
        * axis: Axis
        * fieldStrengthNt: float // Nano teslas
        * t: DateTime
    | Thermal of node: ThermistorNode
        * depthM: float
        * tempC: float
        * t: DateTime
    | Hydrophone of PressureUPa: float * t: DateTime
    | Wind of SpeedMPerS: float
        * DirectionDeg: float
        * t: DateTime
    | AirTemp of tempC: float * t:DateTime
    | Pressure of hPa: float * t:DateTime
    | Generator of generatorRPM: float
        * powerKw: float
        * vibrationMmPerS: float
        * t: DateTime
