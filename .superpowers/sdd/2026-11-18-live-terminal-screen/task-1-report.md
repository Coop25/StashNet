# Task 1 report
Implemented Pos3, ScreenView (clamped heat, equality/operators, NoSignal, ShutOut, HeatWord, HeatSegmentsOf), ScreenOp, ScreenMessage, ScreenCodec in src/StashNet.Core/Protocol/ScreenMessages.cs. Tests: tests/StashNet.Core.Tests/ScreenMessagesTests.cs (6 tests).
RED: `dotnet test tests/StashNet.Core.Tests --filter ScreenMessagesTests` -> CS0246 ScreenView/ScreenMessage not found (types missing, expected).
GREEN: `dotnet test tests/StashNet.Core.Tests` -> Passed 280, Failed 0.
Notes: State read sets HasCore=true; State write requires View (ArgumentException otherwise); heat byte on wire. Unknown op throws FormatException on read and write. Rounding is AwayFromZero.
