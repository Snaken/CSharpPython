using System;
using System.Linq;

namespace CSharpPython.Tests.Runtime
{
    public class HostSignatureTests
    {
        [Test]
        public void TypedParametersAndResultAreRead()
        {
            var s = HostSignature.Parse("kind: str, count: int = 1 -> Entity");
            Assert.That(s.Parameters.Select(p => p.Name), Is.EqualTo(new[] { "kind", "count" }));
            Assert.That(s.Parameters[0].Type, Is.EqualTo(PinType.Str));
            Assert.That(s.Parameters[1].Type, Is.EqualTo(PinType.Int));
            Assert.That(s.Parameters[0].HasDefault, Is.False);
            Assert.That(s.Parameters[1].Default, Is.EqualTo("1"));
            Assert.That(s.HasResult, Is.True);
            Assert.That(s.Outputs.Single().Type, Is.EqualTo(PinType.Handle("Entity")));
            Assert.That(s.Outputs.Single().Name, Is.Null);
            Assert.That(s.ParameterText, Is.EqualTo("kind, count=1"));
        }

        [Test]
        public void UntypedStringsKeepWorkingAndGiveAnyPins()
        {
            var s = HostSignature.Parse("func, /, *args, **keywords");
            Assert.That(s.ParameterText, Is.EqualTo("func, /, *args, **keywords"));
            Assert.That(s.HasResult, Is.False);
            Assert.That(s.Parameters.Select(p => p.Kind), Is.EqualTo(new[]
                { ParameterKind.Normal, ParameterKind.Marker, ParameterKind.VarArgs, ParameterKind.VarKeywords }));
            Assert.That(s.Parameters.Where(p => p.Kind != ParameterKind.Marker).All(p => p.Type == PinType.Any));
            Assert.That(HostSignature.Parse("initial=<missing>").Parameters[0].Default, Is.EqualTo("<missing>"));
            Assert.That(HostSignature.Parse("").Parameters, Is.Empty);
        }

        [Test]
        public void ListTypesNest()
        {
            var s = HostSignature.Parse("items: list[list[int]], any_list: list -> list[str]");
            Assert.That(s.Parameters[0].Type, Is.EqualTo(PinType.ListOf(PinType.ListOf(PinType.Int))));
            Assert.That(s.Parameters[1].Type, Is.EqualTo(PinType.ListOf(PinType.Any)));
            Assert.That(s.Outputs[0].Type.ToString(), Is.EqualTo("list[str]"));
        }

        [Test]
        public void SeveralNamedOutputs()
        {
            var s = HostSignature.Parse("origin: Vec -> (hit: bool, point: Vec)");
            Assert.That(s.Outputs.Select(o => o.Name), Is.EqualTo(new[] { "hit", "point" }));
            Assert.That(s.Outputs.Select(o => o.Type.ToString()), Is.EqualTo(new[] { "bool", "Vec" }));
        }

        [Test]
        public void CommasAndArrowsInsideDefaultsAreNotSplitPoints()
        {
            var s = HostSignature.Parse("tag: str = \"a, b -> c\", n: int = 2");
            Assert.That(s.Parameters.Count, Is.EqualTo(2));
            Assert.That(s.Parameters[0].Default, Is.EqualTo("\"a, b -> c\""));
            Assert.That(s.HasResult, Is.False);
        }

        [Test]
        public void NoneResultIsStillAResult()
        {
            var s = HostSignature.Parse("x: int -> none");
            Assert.That(s.HasResult, Is.True);
            Assert.That(s.Outputs.Single().Type, Is.EqualTo(PinType.None));
        }

        [TestCase("x: -> int")]
        [TestCase("x: int ->")]
        [TestCase("x: list[int -> int")]
        [TestCase("x: dict[int] -> int")]
        [TestCase("-> (a: int, a: int)")]
        [TestCase("-> (int)")]
        [TestCase("-> ()")]
        [TestCase("x: 9bad")]
        public void MalformedTypesAreRejected(string text)
        {
            Assert.Throws<ArgumentException>(() => HostSignature.Parse(text));
        }

        [Test]
        public void DescribeStoresTypesAndKeepsPlainSignatureForTooling()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Native("spawn", (ctx, a) => Value.None, 1, 2)
                .Native("delay", (ctx, a) => Value.None, 1, 1)
                .Describe("spawn", "kind: str, count: int = 1 -> Entity")
                .Describe("delay", "ticks: int", latent: true));
            var spawn = (NativeFunction)vm.Modules["game"].Members["spawn"].RawObject;
            var delay = (NativeFunction)vm.Modules["game"].Members["delay"].RawObject;
            Assert.That(spawn.Signature, Is.EqualTo("kind, count=1"));
            Assert.That(spawn.Typed.Outputs.Single().Type.Name, Is.EqualTo("Entity"));
            Assert.That(spawn.Latent, Is.False);
            Assert.That(delay.Latent, Is.True);
        }
    }
}
