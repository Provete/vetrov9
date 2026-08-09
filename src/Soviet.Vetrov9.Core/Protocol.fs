namespace Soviet.Vetrov9.Core

type ParseError =
    | BadSyncWord
    | ChecksumMismatch
    | Truncated
    | UnknownNode of byte
    | UnknownChannel of byte
    | NodeChannelMismatch of node: byte * channel: byte

module Protocol =
    let nodeAddress (node: PhysicalNode) : byte =
        match node with
        | Domo -> 0x00uy
        | OnSeismicNode (RingA AA1) -> 0x01uy
        | OnSeismicNode (RingA AA2) -> 0x02uy
        | OnSeismicNode (RingA AA3) -> 0x03uy
        | OnSeismicNode (RingA AA4) -> 0x04uy
        | OnSeismicNode (RingB AB1) -> 0x05uy
        | OnSeismicNode (RingB AB2) -> 0x06uy
        | OnSeismicNode (RingB AB3) -> 0x07uy
        | OnSeismicNode (RingB AB4) -> 0x08uy
        | OnThermistorNode Bh1 -> 0x09uy
        | OnThermistorNode Bh2 -> 0x0Auy
        | OnMagnetometerNode Mag1 -> 0x0Buy
        | OnMagnetometerNode Mag2 -> 0x0Cuy
