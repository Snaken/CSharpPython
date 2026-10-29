using System.Linq;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class TypedSignaturesTests
    {
        [Test]
        public void EveryTypedEntryHasTheSameParameterListAsTheSignatureTable()
        {
            foreach (var key in TypedSignatures.Keys)
            {
                Assert.That(Signatures.Find(key), Is.Not.Null, key + " has no plain entry");
                Assert.That(TypedSignatures.Find(key).ParameterText, Is.EqualTo(Signatures.Find(key)), key);
            }
        }

        [Test]
        public void EveryBuiltinFunctionAndMathAndRandomFunctionIsTyped()
        {
            var vm = new PyVm();
            foreach (var module in new[] { "math", "random" })
                foreach (var kv in vm.Modules[module].Members.Where(m => m.Value.Kind == ValueKind.Native && !m.Key.StartsWith('_')))
                    Assert.That(TypedSignatures.Find(module + "." + kv.Key)?.HasResult, Is.True, module + "." + kv.Key);
            foreach (var kv in vm.Builtins.Where(b => b.Value.Kind == ValueKind.Native && Signatures.Find(b.Key) != null))
                Assert.That(TypedSignatures.Find(kv.Key)?.HasResult, Is.True, kv.Key);
        }

        [Test]
        public void TypesAreWhatTheFunctionsTake()
        {
            Assert.That(TypedSignatures.Find("len").Outputs.Single().Type, Is.EqualTo(PinType.Int));
            Assert.That(TypedSignatures.Find("math.sqrt").Parameters[0].Type, Is.EqualTo(PinType.Float));
            Assert.That(TypedSignatures.Find("math.modf").Outputs.Select(o => o.Name), Is.EqualTo(new[] { "fractional", "whole" }));
            Assert.That(TypedSignatures.Find("random.random").Parameters, Is.Empty);
            Assert.That(TypedSignatures.Find("random.shuffle").Parameters[0].Type, Is.EqualTo(PinType.ListOf(PinType.Any)));
        }

        [Test]
        public void ForPrefersTheHostDescriptionThenTheTableThenAnyPins()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Native("spawn", (c, a) => Value.None, 1, 2)
                .Native("plain", (c, a) => Value.None, 1, 2)
                .Describe("spawn", "kind: str -> Entity"));
            var spawn = (NativeFunction)vm.Modules["game"].Members["spawn"].RawObject;
            var plain = (NativeFunction)vm.Modules["game"].Members["plain"].RawObject;
            var len = (NativeFunction)vm.Builtins["len"].RawObject;

            Assert.That(TypedSignatures.For(spawn, "game.spawn", 0).Outputs.Single().Type.Name, Is.EqualTo("Entity"));
            Assert.That(TypedSignatures.For(len, "len", 0).Outputs.Single().Type, Is.EqualTo(PinType.Int));
            var fallback = TypedSignatures.For(plain, "game.plain", 0);
            Assert.That(fallback.HasResult, Is.False);
            Assert.That(fallback.Parameters.All(p => p.Type == PinType.Any));
        }
    }
}
