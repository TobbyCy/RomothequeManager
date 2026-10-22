/* T4-B: immutable illustrative copy of PR #29 fixture blob 1ffc52863ce86ab9a8b79d6b3b31ef5c7c9f8e37. Not a PS5.1-produced export. */
(function(root){"use strict";
 const catalogue={
  "schemaVersion": "0.2.0-ui-catalogue-candidate",
  "readOnly": true,
  "registry": {
    "schemaVersion": "0.1.0-synthetic",
    "readOnly": true,
    "libraries": [
      {
        "libraryId": "lib-t3-ref",
        "name": "Bibliothèque synthétique T3",
        "platforms": [
          "GBA"
        ],
        "sources": [
          {
            "sourceKey": "source-a",
            "rootKey": "root-a",
            "domains": [
              "ROMS"
            ]
          }
        ],
        "currentAvailability": "OFFLINE",
        "availabilityObservedAtUtc": "2026-10-21T10:00:00Z",
        "freshness": "STALE",
        "indexAccessible": true,
        "lastVerifiedCompleteRef": "snap-ref",
        "lastObservedAtUtc": "2026-10-21T12:00:00Z",
        "generations": [
          {
            "snapshotId": "snap-ref",
            "scanId": "scan-ref",
            "generationId": "gen-ref",
            "capturedAtUtc": "2026-10-21T12:00:00Z",
            "schemaVersion": "1.0.0",
            "scanStatus": "complete",
            "scopeIsExhaustive": true,
            "coverage": [
              {
                "console": "GBA",
                "status": "complete",
                "reason": "ALL_DECLARED_PARTITIONS_PROVEN"
              }
            ],
            "coverageProof": [
              {
                "console": "GBA",
                "sourceKey": "source-a",
                "rootKey": "root-a",
                "domain": "ROMS",
                "status": "complete"
              }
            ],
            "countKnown": true,
            "observedUnitCount": 2
          }
        ]
      }
    ]
  },
  "games": [
    {
      "libraryId": "lib-t3-ref",
      "gameId": "game-ref",
      "platform": "GBA",
      "title": "Étoile fictive",
      "quality": "STALE",
      "status": "KNOWN",
      "coverStatus": "KNOWN",
      "availability": "OFFLINE",
      "freshness": "STALE",
      "variants": [
        {
          "variantId": "var-eu",
          "label": "var-eu"
        }
      ],
      "provenance": [
        {
          "libraryId": "lib-t3-ref",
          "snapshotId": "snap-ref",
          "generationId": "gen-ref",
          "observedAtUtc": "2026-10-21T12:00:00Z",
          "source": "SYNTHETIC_SNAPSHOT"
        }
      ],
      "observations": [
        {
          "libraryId": "lib-t3-ref",
          "snapshotId": "snap-ref",
          "generationId": "gen-ref",
          "romUnitId": "disc-1",
          "variantId": "var-eu",
          "discNumber": 1,
          "observedTitle": "Étoile fictive",
          "observationStatus": "OBSERVED"
        },
        {
          "libraryId": "lib-t3-ref",
          "snapshotId": "snap-ref",
          "generationId": "gen-ref",
          "romUnitId": "disc-2",
          "variantId": "var-eu",
          "discNumber": 2,
          "observedTitle": "Étoile fictive",
          "observationStatus": "OBSERVED"
        }
      ]
    }
  ],
  "diagnostics": [],
  "countKnown": true,
  "gameCount": 1
};
 root.RM_PROJECTION_DATA=Object.freeze({registry:catalogue.registry,catalogue,comparisons:[]});
})(typeof window!=="undefined"?window:globalThis);
