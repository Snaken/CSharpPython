using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class DottedImportTests
    {
        private static void Setup(PyVm vm)
        {
            vm.RegisterModule("game.ai", m => m.Function("think", () => Value.FromString("hmm")).Constant("LEVEL", Value.FromInt(3)));
            vm.RegisterModule("game.ai.paths", m => m.Function("find", (a, b) => Value.FromInt(a.AsInt() + b.AsInt())));
            vm.RegisterModule("game", m => m.Constant("NAME", Value.FromString("demo")));
        }

        private static string Go(string source) => Run(source, Setup).TrimEnd('\n');

        [Test]
        public void ImportBindsTheTopLevelPackageAndReachesTheModuleThroughIt()
        {
            Assert.That(Go("import game.ai\nprint(game.ai.think(), game.ai.LEVEL, game.NAME)"), Is.EqualTo("hmm 3 demo"));
        }

        [Test]
        public void DeepImportsBindTheTopAndSetEveryLevel()
        {
            Assert.That(Go("import game.ai.paths\nprint(game.ai.paths.find(1, 2), game.ai.think())"), Is.EqualTo("3 hmm"));
        }

        [Test]
        public void AliasBindsTheNamedModule()
        {
            Assert.That(Go("import game.ai.paths as p\nprint(p.find(2, 5))"), Is.EqualTo("7"));
            Assert.That(Fails("import game.ai.paths as p\nprint(game)", Setup).TypeName, Is.EqualTo("NameError"));
        }

        [Test]
        public void FromImportWorksWithDottedModulesAndSubmodules()
        {
            Assert.That(Go("from game.ai import think, LEVEL as lv\nprint(think(), lv)"), Is.EqualTo("hmm 3"));
            Assert.That(Go("from game import ai\nprint(ai.think())"), Is.EqualTo("hmm"));
            Assert.That(Go("from game.ai import paths\nprint(paths.find(1, 1))"), Is.EqualTo("2"));
            Assert.That(Go("from game.ai.paths import find\nprint(find(4, 4))"), Is.EqualTo("8"));
        }

        [Test]
        public void SubmoduleAttributeIsReachableWithoutImportingItFirst()
        {
            Assert.That(Go("import game\nprint(game.ai.LEVEL)"), Is.EqualTo("3"));
        }

        [Test]
        public void MissingModulesAndMembersRaiseImportError()
        {
            Assert.That(Fails("import game.nothing", Setup).Problem, Is.EqualTo("No module named 'game.nothing'"));
            Assert.That(Fails("import other.thing", Setup).TypeName, Is.EqualTo("ImportError"));
            Assert.That(Fails("from game.ai import missing", Setup).TypeName, Is.EqualTo("ImportError"));
            Assert.That(Fails("from game.nothing import x", Setup).TypeName, Is.EqualTo("ImportError"));
        }

        [Test]
        public void ParentPackagesAreCreatedWhenOnlyAChildIsRegistered()
        {
            var vm = new PyVm();
            vm.RegisterModule("pkg.sub.leaf", m => m.Constant("X", Value.FromInt(9)));
            Assert.That(vm.Modules.ContainsKey("pkg"), Is.True);
            Assert.That(vm.Modules.ContainsKey("pkg.sub"), Is.True);
            var task = vm.Start("import pkg.sub.leaf\nvalue = pkg.sub.leaf.X");
            task.RunUntilDone();
            Assert.That(task.Globals["value"].AsInt(), Is.EqualTo(9));
        }

        [Test]
        public void RegisteringAParentAfterItsChildrenKeepsTheLinks()
        {
            var vm = new PyVm();
            vm.RegisterModule("a.b", m => m.Constant("X", Value.FromInt(1)));
            vm.RegisterModule("a", m => m.Constant("Y", Value.FromInt(2)));
            var task = vm.Start("import a.b\nvalue = a.b.X + a.Y");
            task.RunUntilDone();
            Assert.That(task.Globals["value"].AsInt(), Is.EqualTo(3));
        }

        [Test]
        public void ReRegisteringAChildReplacesTheLink()
        {
            var vm = new PyVm();
            vm.RegisterModule("a.b", m => m.Constant("X", Value.FromInt(1)));
            vm.RegisterModule("a.b", m => m.Constant("X", Value.FromInt(5)));
            var task = vm.Start("import a.b\nvalue = a.b.X");
            task.RunUntilDone();
            Assert.That(task.Globals["value"].AsInt(), Is.EqualTo(5));
        }

        [Test]
        public void ImportInsideAFunctionBindsLocally()
        {
            Assert.That(Go("def f():\n    import game.ai\n    return game.ai.think()\nprint(f())"), Is.EqualTo("hmm"));
        }

        [Test]
        public void DottedNamesParse()
        {
            var module = Parser.ParseModule("import a.b.c\nimport a.b as x\nfrom a.b.c import d\n");
            Assert.That(((ImportStmt)module.Body[0]).Module, Is.EqualTo("a.b.c"));
            Assert.That(((ImportStmt)module.Body[0]).BoundName, Is.EqualTo("a"));
            Assert.That(((ImportStmt)module.Body[1]).Alias, Is.EqualTo("x"));
            Assert.That(((ImportFromStmt)module.Body[2]).Module, Is.EqualTo("a.b.c"));
        }

        [Test]
        public void ADottedImportStillBindsOnlyTheTopNameForNameErrors()
        {
            Assert.That(Fails("import game.ai\nprint(ai)", Setup).TypeName, Is.EqualTo("NameError"));
        }
    }
}
