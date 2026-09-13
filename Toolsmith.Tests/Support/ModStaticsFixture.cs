using Toolsmith.Config;
using Vintagestory.API.Common;

namespace Toolsmith.Tests.Support;

//The define-verification code reports through ToolsmithModSystem.Logger and reads ToolsmithModSystem.Stats,
//both plain mutable statics the game assigns at load. A test has to set them, which makes any two such tests
//running at once a race - hence the collection below, which xUnit runs serially.
//
//Restores whatever was there on dispose so these tests cannot leak a logger into the rest of the suite.
public sealed class ModStaticsScope : IDisposable {

    private readonly ILogger previousLogger;
    private readonly ToolsmithPartStats previousStats;

    public RecordingLogger Log { get; }
    public ToolsmithPartStats Stats { get; }

    public ModStaticsScope(bool enableEdits = false) {
        previousLogger = ToolsmithModSystem.Logger;
        previousStats = ToolsmithModSystem.Stats;

        var (logger, recorder) = RecordingLogger.Create();
        Log = recorder;
        Stats = new ToolsmithPartStats { EnableEdits = enableEdits };

        ToolsmithModSystem.Logger = logger;
        ToolsmithModSystem.Stats = Stats;
    }

    public void Dispose() {
        ToolsmithModSystem.Logger = previousLogger;
        ToolsmithModSystem.Stats = previousStats;
    }
}

[CollectionDefinition("ModStatics", DisableParallelization = true)]
public class ModStaticsCollection { }
