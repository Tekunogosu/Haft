using System.Reflection;
using Vintagestory.API.Common;

namespace Toolsmith.Tests.Support;

//Captures what the mod logs, so a test can assert that a bad config entry was actually reported rather than
//only that it was dropped. Several of these paths keep loading and say so in the log, and the saying so is the
//whole behaviour - a silent drop and a reported one look identical from the resulting dictionary alone.
//
//A DispatchProxy for the same reason FakeWorld is one: ILogger declares 33 members and these tests care about
//the message text, not which severity method carried it.
public class RecordingLogger : DispatchProxy {

    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public static (ILogger logger, RecordingLogger recorder) Create() {
        var proxy = Create<ILogger, RecordingLogger>();
        return ((ILogger)proxy, (RecordingLogger)(object)proxy);
    }

    //True when any error mentions the given text. Matching on a substring rather than the whole message keeps
    //these tests from failing over a reworded sentence, while still pinning that the right subject was named.
    public bool ErrorMentions(string text) => Errors.Any(e => e.Contains(text, StringComparison.Ordinal));

    public bool WarningMentions(string text) => Warnings.Any(w => w.Contains(text, StringComparison.Ordinal));

    protected override object Invoke(MethodInfo targetMethod, object[] args) {
        var message = args is { Length: > 0 } && args[0] is string text ? text : "";

        switch (targetMethod.Name) {
            case "Error":
            case "Fatal":
                Errors.Add(message);
                break;
            case "Warning":
                Warnings.Add(message);
                break;
        }

        //Every ILogger member returns void or a bool property; anything unrecognised is a log call this does
        //not separate out, which is fine to swallow.
        return targetMethod.ReturnType == typeof(bool) ? false : null;
    }
}
