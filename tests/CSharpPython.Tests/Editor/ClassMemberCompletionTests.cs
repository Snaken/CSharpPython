using System.Linq;
using CSharpPython.Editor;

namespace CSharpPython.Tests.Editor
{
    public class ClassMemberCompletionTests
    {
        private static CompletionList At(string marked)
        {
            int cursor = marked.IndexOf('|');
            return Completer.Complete(marked.Remove(cursor, 1), cursor, new PyVm());
        }

        private static string[] Names(string marked) => At(marked).Items.Select(i => i.Text).ToArray();

        private const string Enemy =
            "class Enemy:\n    kind = \"orc\"\n    def __init__(self, hp):\n        self.hp = hp\n        self.rage = 0\n    def hit(self, n):\n        self.hp -= n\n    def heal(self):\n        pass\n";

        [Test]
        public void SelfOffersMethodsClassVariablesAndAssignedAttributes()
        {
            var items = At(Enemy + "    def show(self):\n        self.|\n").Items;
            Assert.That(items.Select(i => i.Text), Is.EquivalentTo(new[] { "__init__", "heal", "hit", "hp", "kind", "rage", "show" }));
            Assert.That(items.Single(i => i.Text == "hit").Kind, Is.EqualTo(CompletionKind.Method));
            Assert.That(items.Single(i => i.Text == "hit").Detail, Is.EqualTo("def hit(self, n)"));
            Assert.That(items.Single(i => i.Text == "hp").Detail, Is.EqualTo("attribute"));
            Assert.That(items.Single(i => i.Text == "kind").Detail, Is.EqualTo("str"));
            Assert.That(items.Single(i => i.Text == "rage").Kind, Is.EqualTo(CompletionKind.Variable));
        }

        [Test]
        public void SelfIsFilteredByThePrefix()
        {
            Assert.That(Names(Enemy + "    def show(self):\n        self.h|\n"), Is.EquivalentTo(new[] { "heal", "hit", "hp" }));
        }

        [Test]
        public void SelfIncludesInheritedMembers()
        {
            string src = "class Base:\n    def __init__(self):\n        self.id = 1\n    def ping(self):\n        pass\nclass Mid(Base):\n    def mid(self):\n        pass\nclass Leaf(Mid):\n    def go(self):\n        self.|\n";
            Assert.That(Names(src), Is.EquivalentTo(new[] { "__init__", "go", "id", "mid", "ping" }));
        }

        [Test]
        public void SuperOffersOnlyTheBaseClassMembers()
        {
            string src = "class Base:\n    def __init__(self):\n        self.id = 1\n    def ping(self):\n        pass\nclass Leaf(Base):\n    def go(self):\n        super().|\n";
            Assert.That(Names(src), Is.EquivalentTo(new[] { "__init__", "id", "ping" }));
            Assert.That(Names("class A:\n    def f(self):\n        super().|\n"), Is.Empty);
        }

        [Test]
        public void SuperInAnExceptionSubclassOffersArgs()
        {
            Assert.That(Names("class MyError(Exception):\n    def __init__(self, msg):\n        super().|\n"), Is.EqualTo(new[] { "args" }));
        }

        [Test]
        public void SuperOutsideAClassFallsBackToTheGeneralRules()
        {
            Assert.That(Names("super().|"), Does.Contain("append"));
            Assert.That(Names("x.super().|"), Does.Contain("append"));
        }

        [Test]
        public void MultipleAndUnknownBasesAreHandled()
        {
            string src = "class A:\n    def a(self):\n        pass\nclass B:\n    def b(self):\n        pass\nclass C(A, B, Missing, 3):\n    def f(self):\n        super().|\n";
            Assert.That(Names(src), Is.EquivalentTo(new[] { "a", "b" }));
        }

        [Test]
        public void CyclicInheritanceDoesNotLoopForever()
        {
            string src = "class A(B):\n    def a(self):\n        self.|\nclass B(A):\n    def b(self):\n        pass\n";
            Assert.That(Names(src), Is.EquivalentTo(new[] { "a", "b" }));
        }

        [Test]
        public void InstancesOfScriptClassesOfferTheirMembers()
        {
            Assert.That(Names(Enemy + "e = Enemy(3)\ne.|\n"), Is.EquivalentTo(new[] { "__init__", "heal", "hit", "hp", "kind", "rage" }));
            Assert.That(Names(Enemy + "e = Enemy(3)\ne.h|\n"), Is.EquivalentTo(new[] { "heal", "hit", "hp" }));
        }

        [Test]
        public void InstancesInsideFunctionsAreResolved()
        {
            Assert.That(Names(Enemy + "def make():\n    foe = Enemy(1)\n    foe.he|\n"), Is.EqualTo(new[] { "heal" }));
        }

        [Test]
        public void OnlyAWholeConstructorCallCounts()
        {
            Assert.That(Names(Enemy + "e = Enemy(3) + 1\ne.|\n"), Does.Contain("append"));
            Assert.That(Names(Enemy + "e = Enemy\ne.|\n"), Does.Contain("append"));
            Assert.That(Names(Enemy + "e = Enemy(\ne.|\n"), Does.Contain("append"));
        }

        [Test]
        public void BuiltinConstructorsKeepTheirMethodCompletion()
        {
            Assert.That(Names("xs = list()\nxs.app|"), Does.Contain("append"));
            Assert.That(Names("x = unknown_call(1)\nx.|"), Does.Contain("append"));
        }

        [Test]
        public void ExceptionVariablesOfferArgs()
        {
            Assert.That(Names("try:\n    pass\nexcept ValueError as e:\n    e.|\n"), Is.EqualTo(new[] { "args" }));
            Assert.That(Names("try:\n    pass\nexcept (A, B) as e:\n    e.|\n"), Is.EqualTo(new[] { "args" }));
            Assert.That(Names("e = ValueError('x')\ne.|\n"), Is.EqualTo(new[] { "args" }));
        }

        [Test]
        public void ScriptExceptionClassesOfferTheirOwnMembersAndArgs()
        {
            string src = "class MyError(Exception):\n    def code(self):\n        return 1\ntry:\n    pass\nexcept MyError as e:\n    e.|\n";
            Assert.That(Names(src), Is.EquivalentTo(new[] { "args", "code" }));
        }

        [Test]
        public void AttributesOfAttributesAreNotGuessed()
        {
            Assert.That(Names(Enemy + "e = Enemy(1)\ne.hp.|\n"), Is.Empty);
        }

        [Test]
        public void OtherNamesStillGetTheGeneralRules()
        {
            Assert.That(Names("s = \"a\"\ns.up|"), Is.EqualTo(new[] { "upper" }));
            Assert.That(Names("undefined.|"), Does.Contain("append"));
            Assert.That(Names("def f(x):\n    x.|\n"), Does.Contain("append"));
            Assert.That(Names("class A:\n    def f(me):\n        me.|\n"), Does.Contain("append"));
            Assert.That(Names("def f(self):\n    self.|\n"), Does.Contain("append"));
            Assert.That(Names(".|"), Is.Empty);
        }

        [Test]
        public void NestedClassesAndLaterClassesAreSeen()
        {
            string src = "class Outer:\n    class Inner:\n        def deep(self):\n            pass\n    def f(self):\n        self.|\n";
            Assert.That(Names(src), Is.EquivalentTo(new[] { "Inner", "f" }));
            Assert.That(At(src).Items.Single(i => i.Text == "Inner").Kind, Is.EqualTo(CompletionKind.Class));
            Assert.That(Names("class Outer:\n    class Inner:\n        def deep(self):\n            self.|\n"), Is.EqualTo(new[] { "deep" }));
        }

        [Test]
        public void HalfTypedClassesStillWork()
        {
            Assert.That(Names("class A:\n    def f(self):\n        self.hp = (1 +\n        self.|"), Does.Contain("f"));
        }
    }
}
