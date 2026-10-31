using System.Linq;
using CSharpPython.Blocks;

namespace CSharpPython.Tests.Blocks
{
    public class PinTypeIndexTests
    {
        private static BlockLibrary Library()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Native("spawn", (c, a) => Value.None, 1, 2)
                .Native("damage", (c, a) => Value.None, 2, 2)
                .Native("raycast", (c, a) => Value.None, 1, 1)
                .Native("names", (c, a) => Value.None, 0, 0)
                .Native("untyped", (c, a) => Value.None, 1, 1)
                .Describe("spawn", "kind: str, count: int = 1 -> Entity")
                .Describe("damage", "target: Entity, amount: float -> none")
                .Describe("raycast", "origin: Vec -> (hit: bool, point: Vec)")
                .Describe("names", "-> list[str]")
                .Describe("untyped", "thing"));
            return new BlockLibrary().AddAllModules(vm);
        }

        private static string[] Names(System.Collections.Generic.IEnumerable<PinMatch> matches) =>
            matches.Select(m => (m.Function.Module == null ? "" : m.Function.Module + ".") + m.Function.Name + ":" + (m.Pin ?? "")).ToArray();

        [Test]
        public void FunctionsGetPinTypesFromTheirDescription()
        {
            var lib = Library();
            var spawn = lib.FindFunction("game.spawn");
            Assert.That(spawn.ParameterTypes, Is.EqualTo(new[] { PinType.Str, PinType.Int }));
            Assert.That(spawn.Outputs.Single().Type.Name, Is.EqualTo("Entity"));

            var ray = lib.FindFunction("game.raycast");
            Assert.That(ray.Outputs.Select(o => o.Name), Is.EqualTo(new[] { "hit", "point" }));

            Assert.That(lib.FindFunction("game.damage").Outputs, Is.Empty);

            var untyped = lib.FindFunction("game.untyped");
            Assert.That(untyped.ParameterTypes, Is.EqualTo(new[] { PinType.Any }));
            Assert.That(untyped.Outputs.Single().Type, Is.EqualTo(PinType.Any));
        }

        [Test]
        public void BuiltInsAndMathTakeTheirTypesFromTheTable()
        {
            var lib = Library();
            Assert.That(lib.FindFunction("len").Outputs.Single().Type, Is.EqualTo(PinType.Int));
            Assert.That(lib.FindFunction("math.sqrt").ParameterTypes, Is.EqualTo(new[] { PinType.Float }));
        }

        [Test]
        public void AcceptingListsInputsAnOutputFitsInLibraryOrder()
        {
            var index = new PinTypeIndex(Library());
            var accepting = Names(index.Accepting(PinType.Handle("Entity")));
            Assert.That(accepting, Does.Contain("game.damage:arg0"));
            Assert.That(accepting, Does.Contain("game.untyped:arg0"), "an any pin takes anything");
            Assert.That(accepting, Does.Not.Contain("game.spawn:arg0"));
            Assert.That(accepting, Does.Not.Contain("game.damage:arg1"));
        }

        [Test]
        public void AnIntOutputFitsIntAndFloatAndAnyPins()
        {
            var index = new PinTypeIndex(Library());
            var accepting = Names(index.Accepting(PinType.Int));
            Assert.That(accepting, Does.Contain("game.spawn:arg1"));
            Assert.That(accepting, Does.Contain("game.damage:arg1"), "int fits a float pin");
            Assert.That(accepting, Does.Contain("math.sqrt:arg0"));
            Assert.That(accepting, Does.Not.Contain("game.spawn:arg0"));
        }

        [Test]
        public void AFloatOutputDoesNotFitAnIntPin()
        {
            var index = new PinTypeIndex(Library());
            var accepting = Names(index.Accepting(PinType.Float));
            Assert.That(accepting, Does.Contain("game.damage:arg1"));
            Assert.That(accepting, Does.Not.Contain("game.spawn:arg1"));
        }

        [Test]
        public void ProducingListsOutputsThatFitAnInput()
        {
            var index = new PinTypeIndex(Library());
            Assert.That(Names(index.Producing(PinType.Handle("Entity"))), Does.Contain("game.spawn:"));
            Assert.That(Names(index.Producing(PinType.Handle("Entity"))), Does.Contain("game.untyped:"), "an any output fits anything");

            var forFloat = Names(index.Producing(PinType.Float));
            Assert.That(forFloat, Does.Contain("len:"), "int fits a float pin");
            Assert.That(forFloat, Does.Contain("math.sqrt:"));

            var forInt = Names(index.Producing(PinType.Int));
            Assert.That(forInt, Does.Contain("len:"));
            Assert.That(forInt, Does.Not.Contain("math.sqrt:"));
        }

        [Test]
        public void EachOutputPinOfAMultiOutputFunctionIsFoundSeparately()
        {
            var index = new PinTypeIndex(Library());
            var forBool = Names(index.Producing(PinType.Bool));
            Assert.That(forBool, Does.Contain("game.raycast:hit"));
            Assert.That(forBool, Does.Not.Contain("game.raycast:point"));
            Assert.That(Names(index.Producing(PinType.Handle("Vec"))), Does.Contain("game.raycast:point"));
        }

        [Test]
        public void ListPinsMatchOnElementType()
        {
            var index = new PinTypeIndex(Library());
            Assert.That(Names(index.Producing(PinType.Parse("list[str]"))), Does.Contain("game.names:"));
            Assert.That(Names(index.Producing(PinType.Parse("list[int]"))), Does.Not.Contain("game.names:"));
            Assert.That(Names(index.Producing(PinType.Parse("list[any]"))), Does.Contain("game.names:"));
        }

        [Test]
        public void ANoneOutputFitsNothingAndNoneFunctionsHaveNoOutputs()
        {
            var index = new PinTypeIndex(Library());
            Assert.That(index.Accepting(PinType.None), Is.Empty);
            Assert.That(index.Producing(PinType.Any).Where(m => m.Function.Name == "damage"), Is.Empty);
        }

        [Test]
        public void ResultsFollowTheOrderFunctionsWereAdded()
        {
            var lib = Library();
            var position = lib.Functions.Select((f, i) => (f, i)).ToDictionary(x => x.f, x => x.i);
            foreach (var type in new[] { PinType.Any, PinType.Int, PinType.Handle("Entity") })
            {
                var index = new PinTypeIndex(lib);
                var inputs = index.Accepting(type).Select(m => position[m.Function]).ToList();
                var outputs = index.Producing(type).Select(m => position[m.Function]).ToList();
                Assert.That(inputs, Is.Ordered, "Accepting " + type);
                Assert.That(outputs, Is.Ordered, "Producing " + type);
            }
        }

        [TestCase("int")]
        [TestCase("float")]
        [TestCase("str")]
        [TestCase("bool")]
        [TestCase("any")]
        [TestCase("Entity")]
        [TestCase("Vec")]
        [TestCase("list[str]")]
        [TestCase("list[int]")]
        [TestCase("list[any]")]
        public void TheIndexAgreesWithCheckingEveryPin(string text)
        {
            var lib = Library();
            var type = PinType.Parse(text);
            var index = new PinTypeIndex(lib);

            var expectedInputs = lib.Functions
                .SelectMany(f => f.ParameterTypes.Select((t, i) => (f, i, t)))
                .Where(x => PinCompatibility.CanConnect(type, x.t))
                .Select(x => x.f.Name + ":arg" + x.i);
            Assert.That(index.Accepting(type).Select(m => m.Function.Name + ":" + m.Pin), Is.EquivalentTo(expectedInputs));

            var expectedOutputs = lib.Functions
                .SelectMany(f => f.Outputs.Select((o, i) => (f, i, o)))
                .Where(x => PinCompatibility.CanConnect(x.o.Type, type))
                .Select(x => x.f.Name + ":" + x.o.Name);
            Assert.That(index.Producing(type).Select(m => m.Function.Name + ":" + m.Pin), Is.EquivalentTo(expectedOutputs));
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => new PinTypeIndex(null));
            var index = new PinTypeIndex(Library());
            Assert.Throws<System.ArgumentNullException>(() => index.Accepting(null));
            Assert.Throws<System.ArgumentNullException>(() => index.Producing(null));
        }

        // ---- Built-in blocks ----

        private static string[] BlockPins(System.Collections.Generic.IEnumerable<PinMatch> matches) =>
            matches.Where(m => m.Block != null).Select(m => m.Block.Name + ":" + (m.Pin ?? "")).ToArray();

        [Test]
        public void BlocksAreLeftOutUnlessAsked()
        {
            var lib = Library();
            Assert.That(new PinTypeIndex(lib).Accepting(PinType.Int).All(m => m.Function != null && m.Block == null), Is.True);
            Assert.That(new PinTypeIndex(lib).Producing(PinType.Int).All(m => m.Function != null), Is.True);
            var with = new PinTypeIndex(lib, includeBlocks: true);
            Assert.That(with.Accepting(PinType.Int).Any(m => m.Block != null), Is.True);
            Assert.That(with.Accepting(PinType.Int).Where(m => m.Function != null).Select(m => m.Function.Name),
                Is.EqualTo(new PinTypeIndex(lib).Accepting(PinType.Int).Select(m => m.Function.Name)), "the functions come out unchanged");
        }

        [Test]
        public void FunctionsComeBeforeBlocks()
        {
            var matches = new PinTypeIndex(Library(), true).Accepting(PinType.Int);
            int firstBlock = matches.ToList().FindIndex(m => m.Block != null);
            Assert.That(matches.Skip(firstBlock).All(m => m.Block != null), Is.True);
        }

        [Test]
        public void AnIntCanGoToTheTypedInputsOfBlocks()
        {
            var pins = BlockPins(new PinTypeIndex(Library(), true).Accepting(PinType.Int));
            Assert.That(pins, Contains.Item("index:index"));
            Assert.That(pins, Contains.Item("repeat:count"));
            Assert.That(pins, Contains.Item("for_range:stop"));
            Assert.That(pins, Contains.Item("add:a"), "an untyped block input takes anything");
            Assert.That(pins, Does.Not.Contain("index:list"), "a list pin does not take an int");
        }

        [Test]
        public void AListGoesToTheListPinsAndWildcards()
        {
            var pins = BlockPins(new PinTypeIndex(Library(), true).Accepting(PinType.ListOf(PinType.Str)));
            Assert.That(pins, Contains.Item("index:list"));
            Assert.That(pins, Contains.Item("append:list"));
            Assert.That(pins, Contains.Item("choose:a"), "a wildcard pin takes any type");
            Assert.That(pins, Does.Not.Contain("repeat:count"));
        }

        [Test]
        public void BlocksThatProduceAValueAreFoundByType()
        {
            var index = new PinTypeIndex(Library(), true);
            var forBool = BlockPins(index.Producing(PinType.Bool));
            Assert.That(forBool, Contains.Item("eq:"));
            Assert.That(forBool, Contains.Item("not:"));
            Assert.That(forBool, Contains.Item("choose:"), "a wildcard output may be a bool");
            Assert.That(forBool, Does.Not.Contain("repeat:"));
            var forInt = BlockPins(index.Producing(PinType.Int));
            Assert.That(forInt, Contains.Item("for_range:"), "a loop's output is its variable");
            Assert.That(forInt, Contains.Item("repeat:"));
            Assert.That(forInt, Does.Not.Contain("eq:"));
        }

        [Test]
        public void TheLiteralBlockCanBeASourceButNotATarget()
        {
            var index = new PinTypeIndex(Library(), true);
            Assert.That(BlockPins(index.Producing(PinType.Str)), Contains.Item("literal:"));
            Assert.That(BlockPins(index.Accepting(PinType.Str)), Does.Not.Contain("literal:value"));
        }

        [Test]
        public void ABlockMatchKnowsItsPositionAndSpec()
        {
            var match = new PinTypeIndex(Library(), true).Accepting(PinType.Int).First(m => m.Block != null && m.Block.Name == "for_range" && m.Pin == "stop");
            Assert.That(match.Function, Is.Null);
            Assert.That(match.Index, Is.EqualTo(1));
            Assert.That(match.Type, Is.EqualTo(PinType.Int));
            Assert.That(match.Block.Kind, Is.EqualTo(BlockKind.Flow));
        }

        [Test]
        public void ASearchByAWildcardFindsEachPinOnce()
        {
            var found = new PinTypeIndex(Library(), true).Accepting(PinType.Variable("T"));
            Assert.That(found.Count, Is.EqualTo(found.Distinct().Count()));
        }
    }
}
