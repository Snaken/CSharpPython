using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class BuiltinsAuditTests
    {
        [Test]
        public void DirListsAnInstancesFieldsAndItsClassChain()
        {
            Assert.That(Out("class A:\n    k = 1\n    def f(self):\n        pass\nclass B(A):\n    def g(self):\n        pass\nb = B()\nb.z = 1\nprint(dir(b))"),
                Is.EqualTo("['f', 'g', 'k', 'z']"));
            Assert.That(Out("class A:\n    k = 1\nclass B(A):\n    m = 2\nprint(dir(B), dir(A))"), Is.EqualTo("['k', 'm'] ['k']"));
        }

        [Test]
        public void DirListsMethodsOfBuiltinTypes()
        {
            Assert.That(Out("print('append' in dir([]), 'upper' in dir('x'), 'keys' in dir({}), 'add' in dir(set()))"), Is.EqualTo("True True True True"));
            Assert.That(Out("print('add' in dir(frozenset()), 'union' in dir(frozenset()))"), Is.EqualTo("False True"));
            Assert.That(Out("print(dir(1), dir(None))"), Is.EqualTo("[] []"));
        }

        [Test]
        public void DirListsModulesExceptionsAndSlices()
        {
            Assert.That(Out("import math\nprint('sqrt' in dir(math), 'pi' in dir(math))"), Is.EqualTo("True True"));
            Assert.That(Out("try:\n    raise ValueError('x')\nexcept ValueError as e:\n    print(dir(e))"), Is.EqualTo("['args']"));
            Assert.That(Out("print(dir(slice(1)))"), Is.EqualTo("['start', 'step', 'stop']"));
        }

        [Test]
        public void VarsReturnsACopyOfTheNamespace()
        {
            Assert.That(Out("class A:\n    pass\na = A()\na.x = 1\na.y = 2\nv = vars(a)\nv['x'] = 9\nprint(v, a.x)"), Is.EqualTo("{'x': 9, 'y': 2} 1"));
            Assert.That(Out("class A:\n    k = 1\n    def f(self):\n        pass\nprint(sorted(vars(A).keys()))"), Is.EqualTo("['f', 'k']"));
            Assert.That(Out("import math\nprint(vars(math)['pi'] == math.pi)"), Is.EqualTo("True"));
            Assert.That(Fails("vars(1)").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void NewExceptionClassesFormAHierarchy()
        {
            Assert.That(Out("print(issubclass(Exception, BaseException), issubclass(SystemExit, Exception), issubclass(KeyboardInterrupt, Exception), issubclass(GeneratorExit, BaseException))"),
                Is.EqualTo("True False False True"));
            Assert.That(Out("print(issubclass(FileNotFoundError, OSError), issubclass(TimeoutError, OSError), issubclass(ModuleNotFoundError, ImportError), issubclass(IndentationError, SyntaxError), issubclass(UserWarning, Warning), issubclass(FloatingPointError, ArithmeticError), issubclass(UnicodeError, ValueError), issubclass(Warning, Exception))"),
                Is.EqualTo("True True True True True True True True"));
        }

        [Test]
        public void ExceptionClassesCanBeRaisedCaughtAndSubclassed()
        {
            Assert.That(Out("try:\n    raise FileNotFoundError('gone')\nexcept OSError as e:\n    print(type(e).__name__, e)"), Is.EqualTo("FileNotFoundError gone"));
            Assert.That(Out("class MyWarn(UserWarning):\n    pass\ntry:\n    raise MyWarn('w')\nexcept Warning as e:\n    print(type(e).__name__)"), Is.EqualTo("MyWarn"));
            Assert.That(Out("class Quit(BaseException):\n    pass\ntry:\n    raise Quit()\nexcept BaseException as e:\n    print('caught')"), Is.EqualTo("caught"));
        }

        [Test]
        public void ExceptionDoesNotCatchBaseExceptionsThatAreNotExceptions()
        {
            Assert.That(Out("try:\n    try:\n        raise SystemExit()\n    except Exception:\n        print('wrong')\nexcept SystemExit:\n    print('right')"), Is.EqualTo("right"));
            Assert.That(Out("try:\n    try:\n        raise KeyboardInterrupt()\n    except Exception:\n        print('wrong')\nexcept BaseException:\n    print('right')"), Is.EqualTo("right"));
            Assert.That(Fails("raise SystemExit()").TypeName, Is.EqualTo("SystemExit"));
        }

        [Test]
        public void FrozenSetMembersAreCheckedByValueEvenForSets()
        {
            Assert.That(Out("s = {frozenset([1])}\nprint({1} in s)\ns.remove({1})\nprint(len(s))"), Is.EqualTo("True\n0"));
        }

        [Test]
        public void FrozenSetReprNestsAndEmptyFrozenSetIsDistinct()
        {
            Assert.That(Out("print(frozenset([frozenset([1])]), frozenset(), set())"), Is.EqualTo("frozenset({frozenset({1})}) frozenset() set()"));
            Assert.That(Fails("frozenset(1)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("frozenset([[1]])").TypeName, Is.EqualTo("TypeError"));
        }
    }
}
