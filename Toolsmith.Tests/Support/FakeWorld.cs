using System.Reflection;
using Vintagestory.API.Common;

namespace Toolsmith.Tests.Support;

//A stand-in for the game world, existing solely to hand the code under test a Random it can control.
//
//IWorldAccessor declares 97 abstract methods, and the mod's math reaches exactly one property on it. Written as
//a DispatchProxy rather than a hand-written class so the other 96 need no bodies here: a stub listing them all
//would be mostly noise, and would have to be edited every time the game's own interface gained a member.
//Anything the code under test touches beyond Rand throws rather than returning a default, so a test cannot
//quietly pass against a stubbed-out value that a real world would have supplied.
public class FakeWorld : DispatchProxy {

    private Random rand = new Random();

    public static IWorldAccessor WithRandom(Random rand) {
        var proxy = Create<IWorldAccessor, FakeWorld>();
        ((FakeWorld)(object)proxy).rand = rand;
        return proxy;
    }

    //A world whose rolls are repeatable, so a test asserting on a probabilistic outcome means the same thing
    //on every run.
    public static IWorldAccessor Seeded(int seed = 1) => WithRandom(new Random(seed));

    protected override object Invoke(MethodInfo targetMethod, object[] args) {
        if (targetMethod.Name == "get_Rand") {
            return rand;
        }

        throw new NotSupportedException(
            "FakeWorld was asked for " + targetMethod.Name + ", which it does not model. Give it a body here if " +
            "the code under test genuinely needs it, rather than returning a default.");
    }
}

//A Random returning a fixed sequence, so a chance calculation can be probed either side of its own threshold
//rather than sampled until it happens to land.
public class ScriptedRandom : Random {

    private readonly double[] values;
    private int next;

    public ScriptedRandom(params double[] values) {
        this.values = values;
    }

    public override double NextDouble() {
        var value = values[next % values.Length];
        next++;
        return value;
    }
}
