using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class DecoratorTests
    {
        [Test]
        public void DecoratorsMayBeSeparatedFromTheDefByBlankLinesAndComments()
        {
            Assert.That(Out("def d(f):\n    return lambda: f() + 1\n@d\n\n# a comment\n\ndef f():\n    return 1\nprint(f())"), Is.EqualTo("2"));
        }

        [Test]
        public void DecoratorAfterABlockEndsAndInsideANestedBlock()
        {
            const string src = "def d(f):\n    return lambda: 'd' + f()\nif True:\n    x = 1\n@d\ndef a():\n    return 'a'\nif True:\n    @d\n    def b():\n        return 'b'\nprint(a(), b())";
            Assert.That(Out(src), Is.EqualTo("da db"));
        }

        [Test]
        public void ADecoratorThatIsNotAFunctionFailsWhenTheDefRuns()
        {
            var e = Fails("@5\ndef f():\n    pass");
            Assert.That(e.TypeName, Is.EqualTo("TypeError"));
            Assert.That(e.Problem, Does.Contain("not callable"));
        }

        [Test]
        public void TheDecoratorIsEvaluatedBeforeTheFunctionIsMade()
        {
            Assert.That(Fails("@missing\ndef f():\n    pass").TypeName, Is.EqualTo("NameError"));
            Assert.That(Out("log = []\ndef d(f):\n    log.append('apply')\n    return f\ndef make():\n    log.append('make')\n    return d\n@make()\ndef f(a=log.append('default')):\n    pass\nprint(log)"),
                Is.EqualTo("['make', 'default', 'apply']"));
        }

        [Test]
        public void ADecoratorCanBeTheNameOfTheThingBeingDefined()
        {
            Assert.That(Out("def f(g):\n    return g\n@f\ndef f():\n    return 1\nprint(f())"), Is.EqualTo("1"));
        }

        [Test]
        public void ErrorInsideTheDecoratorReportsTheDecoratorLine()
        {
            var e = Fails("def d(f):\n    raise ValueError('no')\n\n@d\ndef f():\n    pass");
            Assert.That(e.TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void DecoratorsWorkOnLambdaReturnedFromFactoriesAndNestedClasses()
        {
            Assert.That(Out("def d(c):\n    c.x = 1\n    return c\nclass Outer:\n    @d\n    class Inner:\n        pass\nprint(Outer.Inner.x)"), Is.EqualTo("1"));
        }

        [Test]
        public void AtOutsideTheStartOfALineIsStillNotSupported()
        {
            Assert.Throws<PySyntaxError>(() => new PyVm().Compile("a = 1\nb = a @ a"));
            Assert.Throws<PySyntaxError>(() => new PyVm().Compile("x = (\n@d)"));
            Assert.Throws<PySyntaxError>(() => new PyVm().Compile("a @= 1"));
        }

        [TestCase("@d\nx = 1", "expected 'def' or 'class' after a decorator")]
        [TestCase("@d", "expected 'def' or 'class' after a decorator")]
        [TestCase("@d\n@e\npass", "expected 'def' or 'class' after a decorator")]
        [TestCase("@d x\ndef f(): pass", "expected a newline after the decorator")]
        [TestCase("@\ndef f(): pass", "unexpected")]
        [TestCase("@d; def f(): pass", "expected a newline after the decorator")]
        public void BadDecoratorSyntaxIsReported(string source, string message)
        {
            var e = Assert.Throws<PySyntaxError>(() => new PyVm().Compile(source));
            Assert.That(e.Problem, Does.Contain(message));
        }

        [Test]
        public void StaticMethodIsUnwrappedOnTheClassAndOnInstances()
        {
            const string src = "class A:\n    @staticmethod\n    def f(a):\n        return a * 2\nprint(A.f(2), A().f(3), type(A.f).__name__)";
            Assert.That(Out(src), Is.EqualTo("4 6 function"));
        }

        [Test]
        public void StaticMethodAcceptsBuiltInCallables()
        {
            Assert.That(Out("class A:\n    size = staticmethod(len)\nprint(A.size([1, 2]), A().size('abc'))"), Is.EqualTo("2 3"));
            Assert.That(Fails("staticmethod(5)").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void ClassMethodNeedsAScriptFunction()
        {
            var e = Fails("classmethod(len)");
            Assert.That(e.TypeName, Is.EqualTo("TypeError"));
            Assert.That(e.Problem, Does.Contain("script function"));
        }

        [Test]
        public void ClassMethodBindsTheClassItWasLookedUpOn()
        {
            const string src = "class A:\n    @classmethod\n    def who(cls):\n        return cls.__name__\nclass B(A):\n    pass\nprint(A.who(), B.who(), B().who(), A().who())";
            Assert.That(Out(src), Is.EqualTo("A B B A"));
        }

        [Test]
        public void ClassMethodCanBeCalledThroughAStoredReference()
        {
            Assert.That(Out("class A:\n    @classmethod\n    def f(cls, x):\n        return cls.__name__ + str(x)\ng = A.f\nprint(g(1))"), Is.EqualTo("A1"));
        }

        [Test]
        public void InstanceAttributeShadowsStaticAndClassMethods()
        {
            Assert.That(Out("class A:\n    @staticmethod\n    def s(): return 1\n    @classmethod\n    def c(cls): return 2\na = A()\na.s = lambda: 10\na.c = lambda: 20\nprint(a.s(), a.c(), A.s(), A.c())"), Is.EqualTo("10 20 1 2"));
        }

        [Test]
        public void PropertyGetterRunsOnEachRead()
        {
            Assert.That(Out("class A:\n    n = 0\n    @property\n    def p(self):\n        A.n += 1\n        return A.n\na = A()\nprint(a.p, a.p, getattr(a, 'p'), hasattr(a, 'p'))"), Is.EqualTo("1 2 3 True"));
        }

        [Test]
        public void ExceptionsFromAPropertyPropagateAndHasattrOnlyHidesAttributeError()
        {
            const string src = "class A:\n    @property\n    def bad(self):\n        raise ValueError('boom')\n    @property\n    def missing(self):\n        raise AttributeError('gone')\nprint(hasattr(A(), 'missing'), getattr(A(), 'missing', 'dflt'))\ntry:\n    hasattr(A(), 'bad')\nexcept ValueError as e:\n    print('ValueError', e)";
            Assert.That(Out(src), Is.EqualTo("False dflt\nValueError boom"));
        }

        [Test]
        public void ReadOnlyPropertyMessages()
        {
            var set = Fails("class A:\n    @property\n    def p(self): return 1\nA().p = 2");
            Assert.That(set.Problem, Is.EqualTo("property 'p' of 'A' object has no setter"));
            var del = Fails("class A:\n    @property\n    def p(self): return 1\ndel A().p");
            Assert.That(del.Problem, Is.EqualTo("property 'p' of 'A' object has no deleter"));
            var get = Fails("class A:\n    p = property()\nA().p");
            Assert.That(get.Problem, Is.EqualTo("property 'p' of 'A' object has no getter"));
            Assert.That(get.TypeName, Is.EqualTo("AttributeError"));
        }

        [Test]
        public void PropertyRejectsNonCallables()
        {
            Assert.That(Fails("property(1)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("property(None, 'x')").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("p = property()\np.setter(5)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Out("p = property(None, None, None)\nprint(type(p).__name__)"), Is.EqualTo("property"));
        }

        [Test]
        public void SetterAndDeleterReturnNewPropertiesAndKeepTheRest()
        {
            const string src = "class A:\n    def g(self): return 'g'\n    def s(self, v): print('s', v)\n    def d(self): print('d')\n    base = property(g)\n    full = base.setter(s).deleter(d)\n    other = base.getter(lambda self: 'other')\na = A()\nprint(a.base, a.full, a.other)\na.full = 1\ndel a.full\ntry:\n    a.base = 1\nexcept AttributeError:\n    print('base still read only')";
            Assert.That(Out(src), Is.EqualTo("g g other\ns 1\nd\nbase still read only"));
        }

        [Test]
        public void PropertyDefinedOnABaseClassAppliesToSubclassInstances()
        {
            const string src = "class A:\n    @property\n    def p(self): return self.__class__.__name__\nclass B(A): pass\nclass C(B):\n    def __init__(self): self.x = 1\nprint(A().p, B().p, C().p, C().x)";
            Assert.That(Out(src), Is.EqualTo("A B C 1"));
        }

        [Test]
        public void PropertyAddedToAClassLaterIsHonoured()
        {
            Assert.That(Out("class A: pass\na = A()\nA.p = property(lambda self: 5)\nprint(a.p)"), Is.EqualTo("5"));
        }

        [Test]
        public void InstanceAttributesSetBeforeAPropertyAppearsAreShadowedByIt()
        {
            Assert.That(Out("class A: pass\na = A()\na.p = 1\nA.p = property(lambda self: 'prop')\nprint(a.p)"), Is.EqualTo("prop"));
        }

        [Test]
        public void PropertyGetterCanUseSuper()
        {
            const string src = "class A:\n    def name(self): return 'A'\nclass B(A):\n    @property\n    def label(self):\n        return super().name() + '!'\nprint(B().label)";
            Assert.That(Out(src), Is.EqualTo("A!"));
        }

        [Test]
        public void PropertyGetterOfASubclassCanReachTheBasePropertyThroughSuper()
        {
            const string src = "class A:\n    @property\n    def v(self): return 1\nclass B(A):\n    @property\n    def v(self): return super().v + 1\nclass C(B):\n    @property\n    def v(self): return super().v * 10\nprint(C().v)";
            Assert.That(Out(src), Is.EqualTo("20"));
        }

        [Test]
        public void SuperInAClassMethodStartsAboveTheOwner()
        {
            const string src = "class A:\n    @classmethod\n    def make(cls): return 'A:' + cls.__name__\nclass B(A):\n    @classmethod\n    def make(cls): return 'B>' + super().make()\nclass C(B): pass\nprint(C.make())";
            Assert.That(Out(src), Is.EqualTo("B>A:C"));
        }

        [Test]
        public void SuperInAClassMethodFindsOnlyClassLevelThings()
        {
            Assert.That(Fails("class A: pass\nclass B(A):\n    @classmethod\n    def m(cls): return super().nothing\nB.m()").TypeName, Is.EqualTo("AttributeError"));
            Assert.That(Out("class A:\n    @property\n    def p(self): return 1\nclass B(A):\n    @classmethod\n    def m(cls): return type(super().p).__name__\nprint(B.m())"), Is.EqualTo("property"));
        }

        [Test]
        public void DescriptorsHaveTypesReprsAndCompareByIdentity()
        {
            const string src = "class A:\n    @staticmethod\n    def s(): pass\n    @classmethod\n    def c(cls): pass\n    @property\n    def p(self): return 1\nprint(type(A.p).__name__, repr(A.p).startswith('<property object at 0x'), A.p == A.p, A.p == property())\nprint(isinstance(A.p, property), isinstance(A.p, object), isinstance(A.s, property))\nprint(issubclass(property, object))";
            Assert.That(Out(src), Is.EqualTo("property True True False\nTrue True False\nTrue"));
            Assert.That(Out("s = staticmethod(len)\nc = classmethod(lambda cls: 1)\nprint(type(s).__name__, type(c).__name__, repr(s)[:20], repr(c)[:20])"),
                Is.EqualTo("staticmethod classmethod <staticmethod object <classmethod object "));
        }

        [Test]
        public void PropertiesAreNotCallable()
        {
            Assert.That(Out("p = property()\nprint(callable(p))"), Is.EqualTo("False"));
            Assert.That(Fails("p = property()\np()").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void PropertyDoesNotInterfereWithOrdinaryAttributes()
        {
            const string src = "class A:\n    x = 1\n    @property\n    def p(self): return 'p'\n    def m(self): return 'm'\na = A()\na.y = 2\nprint(a.x, a.y, a.p, a.m())\na.x = 3\nprint(a.x, A.x)\ndel a.x\nprint(a.x)";
            Assert.That(Out(src), Is.EqualTo("1 2 p m\n3 1\n1"));
        }

        [Test]
        public void FunctionsHaveANameAndMethodsToo()
        {
            Assert.That(Out("def f(): pass\nclass A:\n    def m(self): pass\nprint(f.__name__, A.m.__name__, A().m.__name__, (lambda: 0).__name__)"), Is.EqualTo("f m m <lambda>"));
        }

        [Test]
        public void PropertyReadFromTheHostFailsCleanlyWithoutARunningScript()
        {
            var vm = new PyVm();
            var task = vm.Start("class A:\n    @property\n    def p(self): return 1\n    @p.setter\n    def p(self, v): pass\n    @p.deleter\n    def p(self): pass\na = A()");
            task.Run();
            var a = task.Globals["a"];
            Assert.That(Assert.Throws<PyError>(() => Ops.GetAttr(a, "p")).TypeName, Is.EqualTo("RuntimeError"));
            Assert.That(Assert.Throws<PyError>(() => Ops.SetAttr(a, "p", Value.FromInt(1))).TypeName, Is.EqualTo("RuntimeError"));
            Assert.That(Assert.Throws<PyError>(() => Ops.DelAttr(a, "p")).TypeName, Is.EqualTo("RuntimeError"));
        }

        [Test]
        public void ClassDecoratorsRunOnceAfterTheClassIsBuilt()
        {
            Assert.That(Out("n = []\ndef d(c):\n    n.append(c.__name__)\n    return c\n@d\nclass A:\n    n.append('body')\nprint(n)"), Is.EqualTo("['body', 'A']"));
        }

        [Test]
        public void DecoratedFunctionsCanBeRecursiveAndHaveClosures()
        {
            const string src = "def memo(f):\n    cache = {}\n    def w(n):\n        if n not in cache:\n            cache[n] = f(n)\n        return cache[n]\n    return w\n@memo\ndef fib(n):\n    return n if n < 2 else fib(n - 1) + fib(n - 2)\nprint(fib(30))";
            Assert.That(Out(src), Is.EqualTo("832040"));
        }
    }
}
