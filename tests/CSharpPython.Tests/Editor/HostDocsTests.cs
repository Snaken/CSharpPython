using System;
using System.Linq;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    // The text a host gives Describe (and DescribeParameter) reaches hover, signature help and completion.
    public class HostDocsTests
    {
        private static PyVm Vm()
        {
            var vm = new PyVm();
            vm.RegisterModule("game", m => m
                .Function("spawn", (a, b) => Value.None)
                .Describe("spawn", "kind: str, count: int = 1 -> none", doc: "Puts new entities in the world.")
                .DescribeParameter("spawn", "kind", "The name of the entity.")
                .Function("plain", a => a)
                .Describe("plain", "x"));
            return vm;
        }

        [Test]
        public void HoverShowsTheFunctionsDoc()
        {
            var h = Hover.Get("import game\ngame.spawn(\"a\")\n", 18, Vm());
            Assert.That(h.Documentation, Is.EqualTo("Puts new entities in the world."));
            Assert.That(h.Text, Does.EndWith("Puts new entities in the world."));
            Assert.That(Hover.Get("import game\ngame.plain(1)\n", 18, Vm()).Documentation, Is.Null);
        }

        [Test]
        public void SignatureHelpCarriesTheDocAndEachParametersText()
        {
            string source = "import game\ngame.spawn(";
            var help = SignatureHelper.Get(source, source.Length, Vm());
            Assert.That(help.Documentation, Is.EqualTo("Puts new entities in the world."));
            Assert.That(help.Parameters, Is.EqualTo(new[] { "kind", "count=1" }));
            Assert.That(help.ParameterDocs, Is.EqualTo(new[] { "The name of the entity.", null }));

            string plain = "import game\ngame.plain(";
            var none = SignatureHelper.Get(plain, plain.Length, Vm());
            Assert.That(none.Documentation, Is.Null);
            Assert.That(none.ParameterDocs, Is.EqualTo(new string[] { null }));
        }

        [Test]
        public void CompletionItemsCarryTheDoc()
        {
            var vm = Vm();
            var item = Completer.Complete("import game\ngame.", 17, vm).Items.Single(i => i.Text == "spawn");
            Assert.That(item.Documentation, Is.EqualTo("Puts new entities in the world."));
            Assert.That(item.Detail, Does.StartWith("game.spawn"), "the detail stays a short line");
            Assert.That(Completer.Complete("import game\ngame.", 17, vm).Items.Single(i => i.Text == "plain").Documentation, Is.Null);

            var imported = Completer.Complete("from game import spawn\nsp", 25, vm).Items.Single(i => i.Text == "spawn");
            Assert.That(imported.Documentation, Is.EqualTo("Puts new entities in the world."));
        }

        [Test]
        public void ADocOfOnlyWhitespaceIsNoDoc()
        {
            var vm = new PyVm();
            vm.RegisterModule("g", m => m.Function("f", a => a).Describe("f", "x", doc: "  "));
            Assert.That(((NativeFunction)vm.Modules["g"].Members["f"].RawObject).Doc, Is.Null);
        }

        [Test]
        public void AParameterTextNeedsAParameterOfThatName()
        {
            var vm = new PyVm();
            vm.RegisterModule("g", m =>
            {
                m.Function("f", a => a).Describe("f", "x");
                Assert.Throws<ArgumentException>(() => m.DescribeParameter("f", "y", "nope"));
                Assert.DoesNotThrow(() => m.DescribeParameter("missing", "y", "no such function: ignored"));
                m.Function("u", a => a);
                Assert.Throws<ArgumentException>(() => m.DescribeParameter("u", "a", "not described yet"));
                m.DescribeParameter("f", "x", "the value");
                m.DescribeParameter("f", "x", null);
            });
            Assert.That(((NativeFunction)vm.Modules["g"].Members["f"].RawObject).ParameterDocs, Is.Empty, "null text removes it");
        }
    }
}
