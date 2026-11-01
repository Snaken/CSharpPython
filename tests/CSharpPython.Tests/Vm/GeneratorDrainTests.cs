using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    /// <summary>any, all, sum and list take a generator's values in one run (see PyTask.Drain); these are the limits and edges of that.</summary>
    public class GeneratorDrainTests
    {
        private const string Walk =
            "g = (x for x in range(3000))\n" +
            "print(any(x == 2500 for x in g), next(g))\n" +
            "print(sum(x for x in range(3000)), all(x < 3000 for x in range(3000)), len(list(x for x in range(3000))))\n" +
            "print(sum(0.5 * x for x in range(3000)), any(x < 0 for x in range(3000)))\n";

        [Test]
        public void ASmallSliceGivesTheSameAnswers()
        {
            string expected = Run(Walk);
            Assert.That(Run(Walk, null, new VmOptions { StepsPerSlice = 7 }), Is.EqualTo(expected));
            Assert.That(Run(Walk, null, new VmOptions { StepsPerSlice = 1 }), Is.EqualTo(expected));
        }

        [Test]
        public void AGeneratorOverTheSequenceLimitIsAMemoryErrorAndStaysUsable()
        {
            const string source = "g = (x for x in range(10 ** 6))\ntry:\n    list(g)\nexcept MemoryError as e:\n    print('MemoryError', e, next(g))";
            Assert.That(Out(source), Is.EqualTo("MemoryError sequence too long 100001"));
            Assert.That(Out("g = (x for x in range(5000))\nprint(len(list(g)))"), Is.EqualTo("5000"));
            Assert.That(Run("print(len(list(x for x in range(5000))))", null, new VmOptions { MaxSequenceLength = 5000 }), Is.EqualTo("5000\n"));
            Assert.That(Fails("print(len(list(x for x in range(5001))))", null, new VmOptions { MaxSequenceLength = 5000 }).TypeName, Is.EqualTo("MemoryError"));
        }

        [Test]
        public void AGeneratorThatWaitsInsideADrainIsAnErrorTheScriptCanCatch()
        {
            const string source = "import game\ndef slow():\n    yield 1\n    game.wait(1)\n    yield 2\n" +
                                  "try:\n    sum(slow())\nexcept RuntimeError as e:\n    print('RuntimeError', e)\nprint(sum(x for x in range(4)))";
            string output = Run(source, vm => vm.RegisterModule("game", m => m.Native("wait", (ctx, a) => { ctx.Wait(1); return Value.None; }, 1, 1))).TrimEnd('\n');
            Assert.That(output, Does.Contain("RuntimeError").And.Contain("cannot wait").And.EndWith("6"));
        }

        [Test]
        public void GeneratorsThatDrainGeneratorsTooDeepFailWithARecursionError()
        {
            const string source = "def f(n):\n    if n == 0:\n        return 1\n    return sum(f(n - 1) for _ in range(1))\n" +
                                  "print(f(10))\ntry:\n    f(500)\nexcept RecursionError:\n    print('RecursionError')\nprint(f(10))";
            Assert.That(Out(source), Is.EqualTo("1\nRecursionError\n1"));
        }

        [Test]
        public void TheStepLimitStopsADrainAndLeavesTheTaskFailed()
        {
            foreach (string expression in new[] { "sum(x for x in range(10 ** 12))", "any(x < 0 for x in range(10 ** 12))", "all(x >= 0 for x in range(10 ** 12))" })
                Assert.That(Fails("print(" + expression + ")", null, new VmOptions { MaxSteps = 100000 }).TypeName, Is.EqualTo("StepLimitExceeded"), expression);
        }

        [Test]
        public void AGeneratorThatFinishedWhileDrainedStaysFinished()
        {
            Assert.That(Out("g = (x for x in range(3))\nprint(sum(g), sum(g), list(g), any(g), next(g, 'end'))"), Is.EqualTo("3 0 [] False end"));
        }
    }
}
