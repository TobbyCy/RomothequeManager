# ROMothèque Manager

**English** | [Français](README.md)

**Organize your retro game collection while preserving reliable information about games, their variants, and storage devices.**

ROMothèque Manager is a local catalog and inventory project. It distinguishes EmulationStation Desktop Edition (ES-DE) metadata, recorded inventory observations, and the current availability of storage devices. A disk being offline does not mean that its games have been deleted.

## Current availability

| Feature | Status |
| --- | --- |
| Windows PowerShell 5.1 engine, read-only E1 inventory | Existing code pending integration and technical verification; **not a publicly available application** |
| Dashboard, Collection, game details, and Libraries | [Design documentation](docs/ux/README.md); **not implemented** |
| Export operations, conversion, and online metadata | **No real-world operation has been delivered or authorized** |
| Portable Windows application | **No ZIP, installer, or GitHub Release is available** |
| Synthetic tests | Results must be verified against the final repository; a green CI status does not prove that accessing a real disk is safe |

See the [feature status](docs/product-status.md) and [roadmap](docs/roadmap.md). The next steps are goals, not promised dates or delivered features.

## Using and downloading

**There is currently nothing available to download, install, or run.** The technical scripts must not be tried on a personal collection. A future preview will need to provide a self-contained Windows ZIP, its SHA-256, release notes, and independent verification on another computer without a separately installed .NET runtime. This goal has not yet been achieved.

To explore the planned experience, see the [UX designs](docs/ux/README.md), [glossary](docs/user/lexique.md), and [safety information and limitations](docs/user/securite-et-limites.md).

## Contributing

Contributions are organized through Issues and pull requests. Describe the need, verifiable acceptance criteria, a reproduction using **entirely fictional fixtures**, and the tests actually performed. See [CONTRIBUTING](CONTRIBUTING.md) and the [developer guide](docs/developer/README.md).

For sensitive security reports, read [SECURITY](SECURITY.md) and do not disclose vulnerabilities in an Issue. For questions, see [SUPPORT](SUPPORT.md).

Do not upload any ROMs, BIOS files, real ES-DE XML, private paths, StateRoot data, or user backups to this repository. Visual assets pending rights verification are excluded. No redistribution license has been adopted yet.

**The repository remains private until its public release is explicitly authorized.** No Release has been published.
