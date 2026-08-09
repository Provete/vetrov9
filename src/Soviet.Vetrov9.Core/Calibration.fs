namespace Soviet.Vetrov9.Core

module Calibration =
    let toSeismicSpeed (raw: uint32): float = float raw * 0.998
    let toInfrasoundPressure (raw: uint32): float = float raw * 0.006
    let toMagneticField (raw: uint32): float = float raw * 0.0005
    let toThermalTemp (raw: uint32): float = float raw * 0.005
    let toHydrophonePressure (raw: uint32): float = float raw * 42.0
    let toWindSpeed (pulseCount: uint32): float = float pulseCount * 0.078
    let toBarometricPressure (raw: uint32): float = float raw * 0.01
