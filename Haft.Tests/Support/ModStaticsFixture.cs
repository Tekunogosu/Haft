using Haft.Config;
using Vintagestory.API.Common;

namespace Haft.Tests.Support;

//The define-verification code reports through HaftModSystem.Logger and reads HaftModSystem.Stats,
//both plain mutable statics the game assigns at load. A test has to set them, which makes any two such tests
//running at once a race - hence the collection below, which xUnit runs serially.
//
//Restores whatever was there on dispose so these tests cannot leak a logger into the rest of the suite.
public sealed class ModStaticsScope : IDisposable {

    private readonly ILogger previousLogger;
    private readonly HaftPartStats previousStats;

    public RecordingLogger Log { get; }
    public HaftPartStats Stats { get; }

    public ModStaticsScope(bool enableEdits = false) {
        previousLogger = HaftModSystem.Logger;
        previousStats = HaftModSystem.Stats;

        var (logger, recorder) = RecordingLogger.Create();
        Log = recorder;
        Stats = new HaftPartStats { EnableEdits = enableEdits };

        HaftModSystem.Logger = logger;
        HaftModSystem.Stats = Stats;
    }

    public void Dispose() {
        HaftModSystem.Logger = previousLogger;
        HaftModSystem.Stats = previousStats;
    }
}

[CollectionDefinition("ModStatics", DisableParallelization = true)]
public class ModStaticsCollection { }
