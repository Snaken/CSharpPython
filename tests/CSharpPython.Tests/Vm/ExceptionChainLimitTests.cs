namespace CSharpPython.Tests.Vm
{
    /// <summary>
    /// A script can raise inside an except block without limit, each exception remembering the one before and the call
    /// stack it came from. The chain is cut after 64 links, and exception traces count towards the memory limit.
    /// </summary>
    public class ExceptionChainLimitTests
    {
        private const string Walk =
            "def length(e):\n    n = 0\n    while e is not None:\n        n += 1\n        e = e.__context__\n    return n\n";

        [Test]
        public void ALongChainIsCutAfterSixtyFourLinks()
        {
            string script = Walk +
                "def deep(n):\n    try:\n        raise ValueError(n)\n    except ValueError:\n        if n < 150:\n            deep(n + 1)\n        else:\n            raise KeyError(n)\n" +
                "try:\n    deep(0)\n" +
                "except KeyError as e:\n    print(length(e))\n";
            Assert.That(Py.Out(script), Is.EqualTo("65"));
        }

        [Test]
        public void AShortChainIsKeptWhole()
        {
            string script = Walk +
                "def deep(n):\n    try:\n        raise ValueError(n)\n    except ValueError:\n        if n < 10:\n            deep(n + 1)\n        else:\n            raise KeyError(n)\n" +
                "try:\n    deep(0)\n" +
                "except KeyError as e:\n    print(length(e), e.__context__.args[0], e.__context__.__context__.args[0])\n";
            Assert.That(Py.Out(script), Is.EqualTo("12 10 9"));
        }

        [Test]
        public void ACauseIsNotCut()
        {
            string script =
                "try:\n    try:\n        raise ValueError('a')\n    except ValueError as v:\n        raise KeyError('b') from v\n" +
                "except KeyError as k:\n    print(type(k.__cause__).__name__, k.__cause__.args[0], k.__suppress_context__)\n";
            Assert.That(Py.Out(script), Is.EqualTo("ValueError a True"));
        }

        [Test]
        public void RaisingInsideAnExceptBlockForeverUsesBoundedMemory()
        {
            // The review's repro: with the chain kept whole this used about 900 bytes a step.
            string script = "def f():\n    try:\n        f()\n    except RecursionError:\n        f()\nf()\n";
            var vm = new PyVm(new VmOptions { MaxSteps = 400000, MaxMemoryBytes = 0 });
            var task = vm.Start(script);
            task.RunUntilDone();
            Assert.That(task.Error.TypeName, Is.EqualTo("StepLimitExceeded"));
            Assert.That(task.EstimatedMemoryBytes, Is.LessThan(2L * 1024 * 1024));
        }

        [Test]
        public void ATraceCountsTowardsTheMemoryLimit()
        {
            // 200,000 exceptions kept alive through their causes, each with a short trace.
            string script = "e = None\nfor i in range(200000):\n    try:\n        raise ValueError(i) from e\n    except ValueError as x:\n        e = x\n";
            var vm = new PyVm(new VmOptions { MaxMemoryBytes = 8L * 1024 * 1024 });
            var task = vm.Start(script);
            task.RunUntilDone();
            Assert.That(task.Error?.TypeName, Is.EqualTo("MemoryLimitExceeded"));
        }

        [Test]
        public void ExceptionsThatAreDroppedAreNotCounted()
        {
            string script = "for i in range(100000):\n    try:\n        raise ValueError(i)\n    except ValueError:\n        pass\n";
            Assert.That(new PyVm().Start(script).RunUntilDone(), Is.EqualTo(TaskState.Finished));
        }
    }
}
