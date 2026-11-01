# ESDE-FP-V2 — independent golden vectors (#142)

These vectors are **synthetic and independent of the .NET implementation**. They were
calculated using Python 3 standard-library `struct`, `hashlib`, `base64` with explicit
big-endian field layout. Tests pin **the entire preimage bytes** (base64), not just a
digest copied from production. The contract of record IDs, Unicode, ordered discs
and media sorting is DEV 2's `CONTRACT_APPROVABLE` in Product Issue #142.

The binary preimage begins with ASCII `ROMOTHEQUE:ESDE-FP:V2\0`, then root TLVs:
`(1, string, ES_DE_READ_ONLY)`, `(2, list-of-Game-records)`. A field is
`uint16BE id | uint8 typeTag | value`; string tag 1 has presence `0=null`
or `1 | uint32BE length-in-UTF8-BYTES | strict UTF-8 bytes`; bool tag 2 has
one byte 0/1. List tag 3 has `uint32BE count`; records in a list are
`uint32BE byte-length | record TLVs`, strings in a list are complete
presence/length/value strings. Root has no redundant outer length.
All IDs, list orders and meanings are fixed by the contract (a different
layout requires V3).

**Reference command (portable Python 3, no repository/library access):**

```python
import base64
import hashlib
import struct

magic = b"ROMOTHEQUE:ESDE-FP:V2\x00"

def u32(n):
    return struct.pack(">I", n)

def field(number, tag, payload):
    return struct.pack(">HB", number, tag) + payload

def string(value):
    if value is None:
        return b"\x00"
    content = value.encode("utf-8", "strict")
    return b"\x01" + u32(len(content)) + content

def text(number, value):
    return field(number, 1, string(value))

def boolean(number, value):
    return field(number, 2, bytes([1 if value else 0]))

def records(number, items):
    return field(number, 3, u32(len(items)) +
                 b"".join(u32(len(item)) + item for item in items))

def strings(number, items):
    return field(number, 3, u32(len(items)) +
                 b"".join(string(item) for item in items))

def media(kind, path, present):
    return text(1, kind) + text(2, path) + boolean(3, present)

def game():
    # Synthetic projection; no file/ROM access.
    record = b"".join([
        text(1, "esde-fixture"),
        text(2, "variant-fixture"),
        text(3, "gba"),
        text(4, "École|A\n🎮"),
        text(5, "ROMS/gba/Invented.gba"),
        boolean(6, True),
        text(7, None),
        text(8, ""),
        text(9, "Studio:Test"),
        text(10, None),
    ])
    media_records = [
        media("image", "MEDIAS/gba/covers/à.png", True),
        media("thumbnail", "MEDIAS/gba/covers/none.png", False),
    ]
    return record + records(11, media_records) + strings(
        12, ["ROMS/gba/Disc:1.chd", "ROMS/gba/Disc,2.chd"])

def root(items):
    return magic + text(1, "ES_DE_READ_ONLY") + records(2, items)

for name, preimage in [("empty", root([])), ("unicode", root([game()]))]:
    print(name, "bytes", len(preimage))
    print("base64", base64.b64encode(preimage).decode("ascii"))
    print("sha256", hashlib.sha256(preimage).hexdigest())
```

**Pinned independent output:**

| Case | Bytes | SHA-256 (full lowercase hex) |
| --- | ---: | --- |
| Empty 0 Games | 52 | `8b2c99a55eb9a53f439a59621e839297c11528e42dbd8dc385e2ca48972cd810` |
| Unicode, media sort, ordered discs | 373 | `a0b6e68fd63483448635d4080f5c7dcd82c92f916ce7966bbc599c4b1b99f6d1` |

The two exact base64 preimages are pinned in
`tests/dotnet/RomothequeManager.V2.Tests/EsDeFingerprintV2Tests.cs`.
The output fingerprint is
`esde-fp-v2:sha256:` + the above 64-character lowercase digest, *not*
the 24-hex legacy `Digest` result. In particular `é` is two UTF-8 bytes
and `🎮` four, without implicit Unicode normalization.

**Boundary:** This is a candidate verification artefact on synthetic
projections, not a migration, real-volume scan, or authorization to
relocate an actual library. `LibraryId` and diagnostics are excluded.
The production shared `Digest(string)` and GameId/VariantId derivation
are not modified. V1/unknown/malformed fingerprint sources fail closed,
and `Current` and the chosen root remain unchanged on rejection.
