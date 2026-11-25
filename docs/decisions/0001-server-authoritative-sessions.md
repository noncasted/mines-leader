# 0001. Server-authoritative sessions and one shared protocol

Status: accepted. The overview is in the "Technical decisions" section of the [README](../../README.md); this record
keeps the reasons and the cost.

## Problem

Both players see each other's field live, cards change the opponent's board, and a match changes rating and
progression. A client that decides any of it can be patched in the browser: the WebGL build is a public download.
Two codebases (Unity C# and .NET) that describe the same messages by hand drift apart.

## Decision

- The client sends intents only (open, flag, play a card). The real-time session on the backend validates them, runs
  the board and the card effects, checks the win conditions and pushes property updates back. The session is the
  framework `backend/Session`; this game creates its sessions in `backend/Game/Global/SessionFactory.cs`.
- The bot plays through the same session code as a user with a deck: there is no second rule set for practice.
- `shared/` holds the protocol, the board and card models and the configs. Unity imports it as a local package, the
  backend references it as a csproj. Every message is a MemoryPack union (`shared/Protocol/NetworkContextUnion.cs`).
- Both socket upgrades check `ProtocolHash.Value` (`shared/Protocol/ProtocolHash.cs`), a hash of the golden schema
  file `backend/Tools/Tests/Protocol/protocol_schema.golden.txt`. A client on another protocol is closed with 4001 and
  sees "client outdated" instead of a broken match.

## Numbers

- The wire format is pinned by two golden files: `ProtocolSchemaTests` fails when the schema changes without a new
  hash; the backend `ProtocolBytesTests` writes the bytes of every shared type to a golden file, and the client
  EditMode `Internal.Tests.ProtocolBytesTests` reads them with the trimmed client MemoryPack and writes them back
  byte for byte.
- Latency and server cost per match: not measured yet. How: the match metrics of the framework
  (`/api/match-metrics`, skill `match-history`) record the turns with their time per match.

## Cost

- Every action waits for a round trip to the server before its result is known.
- Every protocol change is a lockstep deploy: the backend first, then the client. `/deploy-client` waits in its
  `beforeUpload` hook until the live backend accepts the hash of the tree.
- The shared code is limited to what both sides compile: Unity compiles C# 9, so `shared/` cannot use newer language
  features.
