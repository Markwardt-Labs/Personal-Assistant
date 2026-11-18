#:package Markwardt.ScriptUtilities@0.2.0
#:property TreatWarningsAsErrors=true

// Runs the server locally with a throwaway dev config (access key "dev") under Scripts/Support/Data/,
// rewritten on each start and git-ignored. The Android emulator reaches the host at 10.0.2.2. Supports
// Run.task and is not run directly. File-based app (dotnet run) - run from the repo root, e.g. `dotnet
// run Scripts/Support/Server.cs`.

using Markwardt.ScriptUtilities;

string data = Path.GetFullPath("Scripts/Support/Data");

Directory.CreateDirectory(data);
File.WriteAllText(Path.Combine(data, "config.json"), """{ "port": 52680, "key": "dev" }""");

Script.Log("Dev server: port 52680, access key \"dev\" (10.0.2.2 from the emulator).");
(await Script.Run("dotnet", "run", "--project", "Server/Server.csproj", "--", "--data-folder", data)).Verify();
