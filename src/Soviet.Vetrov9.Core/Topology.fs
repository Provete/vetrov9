namespace Soviet.Vetrov9.Core


type Coordinate = float * float

module Topology =
    let coordinatesOf (node: PhysicalNode) : Coordinate =
        match node with
        | Domo -> (0.0, 0.0)
        | OnSeismicNode (RingA AA1) -> (0.0, 500.0)
        | OnSeismicNode (RingA AA2) -> (500.0, 0.0)
        | OnSeismicNode (RingA AA3) -> (0.0, -500.0)
        | OnSeismicNode (RingA AA4) -> (-500.0, 0.0)
        | OnSeismicNode (RingB AB1) -> (1556.0, 1556.0)
        | OnSeismicNode (RingB AB2) -> (1556.0, -1556.0)
        | OnSeismicNode (RingB AB3) -> (-1556.0, -1556.0)
        | OnSeismicNode (RingB AB4) -> (-1556.0, 1556.0)
        | OnMagnetometerNode Mag1 -> (0.0, 0.0)
        | OnMagnetometerNode Mag2 -> (1800.0, 0.0)
        | OnThermistorNode Bh1 -> (50.0, -30.0)
        | OnThermistorNode Bh2 -> (30.0, 40.0)

    let distanceBetween (a: PhysicalNode) (b: PhysicalNode) : float =
        let x1, y1 = coordinatesOf a
        let x2, y2 = coordinatesOf b
        sqrt ((x2 - x1) ** 2.0 + (y2 - y1) ** 2.0)
