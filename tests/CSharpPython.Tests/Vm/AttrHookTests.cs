using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class AttrHookTests
    {
        [Test]
        public void GetattrReceivesTheNameAsAString()
        {
            Assert.That(Out("class A:\n    def __getattr__(self, n):\n        return type(n).__name__ + ':' + n\nprint(A().abc)"), Is.EqualTo("str:abc"));
        }

        [Test]
        public void GetattrIsNotCalledForFoundAttributes()
        {
            const string src = "class A:\n    c = 1\n    def __init__(self): self.i = 2\n    def __getattr__(self, n):\n        raise ValueError('hook ran')\na = A()\nprint(a.c, a.i)\nprint(a.__class__.__name__)";
            Assert.That(Out(src), Is.EqualTo("1 2\nA"));
        }

        [Test]
        public void ErrorsOtherThanAttributeErrorPropagateFromGetattr()
        {
            Assert.That(Fails("class A:\n    def __getattr__(self, n):\n        raise KeyError(n)\nA().x").TypeName, Is.EqualTo("KeyError"));
        }

        [Test]
        public void MethodCallsThroughGetattrWork()
        {
            Assert.That(Out("class A:\n    def __getattr__(self, n):\n        return lambda *a: n + str(a)\nprint(A().hello(1, 2))"), Is.EqualTo("hello(1, 2)"));
        }

        [Test]
        public void ObjectSetattrAndDelattrNeedAStringName()
        {
            var e = Fails("class A: pass\nobject.__setattr__(A(), 1, 2)");
            Assert.That(e.TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("class A: pass\nobject.__delattr__(A(), 1)").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void ObjectSetattrOnAMissingAttributeToDeleteRaises()
        {
            Assert.That(Fails("class A: pass\nobject.__delattr__(A(), 'nope')").TypeName, Is.EqualTo("AttributeError"));
        }

        [Test]
        public void SuperDelattrAndSetattrReachObject()
        {
            const string src = "class A:\n    def __setattr__(self, n, v):\n        super().__setattr__(n, v * 2)\n    def __delattr__(self, n):\n        print('bye', n)\n        super().__delattr__(n)\na = A()\na.x = 4\nprint(a.x)\ndel a.x\nprint(hasattr(a, 'x'))";
            Assert.That(Out(src), Is.EqualTo("8\nbye x\nFalse"));
        }

        [Test]
        public void HooksAssignedToAClassAfterwardsAreHonoured()
        {
            Assert.That(Out("class A: pass\na = A()\nA.__setattr__ = lambda self, n, v: print('set', n)\na.q = 1\nprint(hasattr(a, 'q'))"), Is.EqualTo("set q\nFalse"));
        }

        [Test]
        public void HooksAreInheritedAndOverridable()
        {
            const string src = "class A:\n    def __setattr__(self, n, v):\n        print('A', n)\n        object.__setattr__(self, n, v)\nclass B(A):\n    pass\nclass C(A):\n    def __setattr__(self, n, v):\n        print('C', n)\n        super().__setattr__(n, v)\nB().x = 1\nC().y = 2";
            Assert.That(Out(src), Is.EqualTo("A x\nC y\nA y"));
        }

        [Test]
        public void RecursiveHooksEndInRecursionError()
        {
            Assert.That(Fails("class A:\n    def __setattr__(self, n, v):\n        self.x = v\nA().y = 1").TypeName, Is.EqualTo("RecursionError"));
        }

        [Test]
        public void ObjectSetattrIsAFunctionOfTheObjectType()
        {
            Assert.That(Out("f = object.__setattr__\nclass A: pass\na = A()\nf(a, 'z', 3)\nprint(a.z, callable(f))"), Is.EqualTo("3 True"));
        }

        [Test]
        public void HooksAreNotRunWithoutAScript()
        {
            var vm = new PyVm();
            var task = vm.Start("class A:\n    def __getattr__(self, n): return 1\n    def __setattr__(self, n, v): pass\n    def __delattr__(self, n): pass\na = A()");
            task.Run();
            var a = task.Globals["a"];
            Assert.That(Assert.Throws<PyError>(() => Ops.GetAttr(a, "nope")).TypeName, Is.EqualTo("AttributeError"));
            Ops.SetAttr(a, "direct", Value.FromInt(1));
            Assert.That(Ops.GetAttr(a, "direct").AsInt(), Is.EqualTo(1));
            Ops.DelAttr(a, "direct");
        }

        [Test]
        public void ExceptionClassesCanUseHooksToo()
        {
            Assert.That(Out("class E(Exception):\n    def __getattr__(self, n):\n        return 'dyn'\ntry:\n    raise E('x')\nexcept E as e:\n    print(e.anything, e.args)"), Is.EqualTo("dyn ('x',)"));
        }
    }
}
