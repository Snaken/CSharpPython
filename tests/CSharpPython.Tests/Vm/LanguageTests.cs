using CSharpPython.Syntax;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class LanguageTests
    {
        [TestCase("1 + 2 * 3", "7")]
        [TestCase("(1 + 2) * 3", "9")]
        [TestCase("7 // 2", "3")]
        [TestCase("-7 // 2", "-4")]
        [TestCase("7 // -2", "-4")]
        [TestCase("-7 % 3", "2")]
        [TestCase("7 % -3", "-2")]
        [TestCase("7 / 2", "3.5")]
        [TestCase("2 ** 10", "1024")]
        [TestCase("2 ** -1", "0.5")]
        [TestCase("2 ** 0.5", "1.4142135623730951")]
        [TestCase("-2 ** 2", "-4")]
        [TestCase("1 == 1.0", "True")]
        [TestCase("True + True", "2")]
        [TestCase("'ab' + 'cd'", "abcd")]
        [TestCase("'ab' * 3", "ababab")]
        [TestCase("[1, 2] + [3]", "[1, 2, 3]")]
        [TestCase("[0] * 3", "[0, 0, 0]")]
        [TestCase("(1, 2) + (3,)", "(1, 2, 3)")]
        [TestCase("1 < 2 < 3", "True")]
        [TestCase("1 < 3 < 2", "False")]
        [TestCase("3 > 2 > 1 > 0", "True")]
        [TestCase("1 if 0 else 2", "2")]
        [TestCase("0 or 'x'", "x")]
        [TestCase("0 and 'x'", "0")]
        [TestCase("1 and 'x'", "x")]
        [TestCase("not []", "True")]
        [TestCase("2 in [1, 2]", "True")]
        [TestCase("3 not in [1, 2]", "True")]
        [TestCase("'a' in 'abc'", "True")]
        [TestCase("None is None", "True")]
        [TestCase("1 is not None", "True")]
        [TestCase("[1,2,3][1:]", "[2, 3]")]
        [TestCase("[1,2,3][::-1]", "[3, 2, 1]")]
        [TestCase("'hello'[1:3]", "el")]
        [TestCase("'hello'[-1]", "o")]
        [TestCase("{'a': 1}['a']", "1")]
        [TestCase("-(-3)", "3")]
        [TestCase("+5", "5")]
        [TestCase("0x10 + 0b11 + 0o7", "26")]
        [TestCase("1_000 * 2", "2000")]
        [TestCase("1e3", "1000.0")]
        [TestCase("10 / 4 * 2", "5.0")]
        public void Expressions(string expr, string expected) => Assert.That(Eval(expr), Is.EqualTo(expected));

        [Test]
        public void ChainedComparisonEvaluatesMiddleOnce()
        {
            Assert.That(Out(@"
n = 0
def f():
    global n
    n += 1
    return 5
print(1 < f() < 10, n)"), Is.EqualTo("True 1"));
        }

        [Test]
        public void ShortCircuitSkipsRightSide()
        {
            Assert.That(Out(@"
def boom():
    raise ValueError('no')
print(False and boom(), True or boom())"), Is.EqualTo("False True"));
        }

        [Test]
        public void VariablesAndAugmentedAssignment()
        {
            Assert.That(Out(@"
x = 5
x += 2
x *= 3
x -= 1
x //= 4
x **= 2
x %= 7
print(x)"), Is.EqualTo("4"));
        }

        [Test]
        public void ChainedAssignment()
        {
            Assert.That(Out("a = b = [1]\na.append(2)\nprint(b)"), Is.EqualTo("[1, 2]"));
        }

        [Test]
        public void TupleUnpacking()
        {
            Assert.That(Out("a, b = 1, 2\na, b = b, a\nprint(a, b)"), Is.EqualTo("2 1"));
            Assert.That(Out("[a, (b, c)] = [1, (2, 3)]\nprint(a, b, c)"), Is.EqualTo("1 2 3"));
            Assert.That(Out("a, b, c = 'xyz'\nprint(c)"), Is.EqualTo("z"));
            Assert.That(Out("a, b = range(2)\nprint(a, b)"), Is.EqualTo("0 1"));
        }

        [Test]
        public void UnpackingErrors()
        {
            Assert.That(Fails("a, b = [1]").Problem, Is.EqualTo("not enough values to unpack (expected 2, got 1)"));
            Assert.That(Fails("a, b = [1, 2, 3]").Problem, Is.EqualTo("too many values to unpack (expected 2)"));
            Assert.That(Fails("a, b = 5").Problem, Is.EqualTo("cannot unpack non-iterable int object"));
        }

        [Test]
        public void IfElifElse()
        {
            const string src = @"
def f(n):
    if n < 0:
        return 'neg'
    elif n == 0:
        return 'zero'
    else:
        return 'pos'
print(f(-1), f(0), f(1))";
            Assert.That(Out(src), Is.EqualTo("neg zero pos"));
        }

        [Test]
        public void WhileWithBreakAndContinue()
        {
            Assert.That(Out(@"
i = 0
out = []
while True:
    i += 1
    if i % 2 == 0:
        continue
    if i > 7:
        break
    out.append(i)
print(out)"), Is.EqualTo("[1, 3, 5, 7]"));
        }

        [Test]
        public void ForLoopsOverSequences()
        {
            Assert.That(Out("for x in [1, 2, 3]:\n    print(x)"), Is.EqualTo("1\n2\n3"));
            Assert.That(Out("for c in 'ab':\n    print(c)"), Is.EqualTo("a\nb"));
            Assert.That(Out("for k in {'a': 1, 'b': 2}:\n    print(k)"), Is.EqualTo("a\nb"));
            Assert.That(Out("for i in range(3):\n    print(i)"), Is.EqualTo("0\n1\n2"));
            Assert.That(Out("for a, b in [(1, 2), (3, 4)]:\n    print(a + b)"), Is.EqualTo("3\n7"));
            Assert.That(Out("for i in range(10, 0, -3):\n    print(i)"), Is.EqualTo("10\n7\n4\n1"));
        }

        [Test]
        public void BreakInsideNestedLoopsOnlyLeavesInnerLoop()
        {
            Assert.That(Out(@"
for i in range(3):
    for j in range(3):
        if j == 1:
            break
        print(i, j)"), Is.EqualTo("0 0\n1 0\n2 0"));
        }

        [Test]
        public void BreakInsideForLeavesStackBalanced()
        {
            // Run many iterations: an unbalanced stack would eventually corrupt locals.
            Assert.That(Out(@"
total = 0
for i in range(200):
    for x in [1, 2, 3]:
        if x == 2:
            break
        total += x
print(total)"), Is.EqualTo("200"));
        }

        [Test]
        public void ForLoopVariableSurvivesLoop()
        {
            Assert.That(Out("for i in range(3):\n    pass\nprint(i)"), Is.EqualTo("2"));
        }

        [Test]
        public void FunctionsReturnValues()
        {
            Assert.That(Out("def f(a, b):\n    return a * b\nprint(f(3, 4))"), Is.EqualTo("12"));
            Assert.That(Out("def f():\n    pass\nprint(f())"), Is.EqualTo("None"));
            Assert.That(Out("def f():\n    return\nprint(f())"), Is.EqualTo("None"));
        }

        [Test]
        public void RecursionWorks()
        {
            Assert.That(Out("def fact(n):\n    return 1 if n <= 1 else n * fact(n - 1)\nprint(fact(20))"), Is.EqualTo("2432902008176640000"));
            Assert.That(Out("def fib(n):\n    if n < 2:\n        return n\n    return fib(n-1) + fib(n-2)\nprint(fib(15))"), Is.EqualTo("610"));
        }

        [Test]
        public void DefaultAndKeywordArguments()
        {
            const string src = @"
def f(a, b=2, c=3):
    return (a, b, c)
print(f(1))
print(f(1, 5))
print(f(1, c=9))
print(f(c=1, a=2, b=3))
print(f(a=0))";
            Assert.That(Out(src), Is.EqualTo("(1, 2, 3)\n(1, 5, 3)\n(1, 2, 9)\n(2, 3, 1)\n(0, 2, 3)"));
        }

        [Test]
        public void DefaultsAreEvaluatedOnceAtDefinition()
        {
            Assert.That(Out(@"
def f(x=[]):
    x.append(1)
    return len(x)
print(f(), f(), f())"), Is.EqualTo("1 2 3"));
        }

        [Test]
        public void ArgumentErrors()
        {
            Assert.That(Fails("def f(a):\n    pass\nf()").Problem, Is.EqualTo("f() missing 1 required positional argument: 'a'"));
            Assert.That(Fails("def f(a, b):\n    pass\nf()").Problem, Is.EqualTo("f() missing 2 required positional arguments: 'a' and 'b'"));
            Assert.That(Fails("def f(a, b, c):\n    pass\nf()").Problem, Is.EqualTo("f() missing 3 required positional arguments: 'a', 'b', and 'c'"));
            Assert.That(Fails("def f(a):\n    pass\nf(1, 2)").Problem, Is.EqualTo("f() takes 1 positional argument but 2 were given"));
            Assert.That(Fails("def f(a, b=1):\n    pass\nf(1, 2, 3)").Problem, Is.EqualTo("f() takes from 1 to 2 positional arguments but 3 were given"));
            Assert.That(Fails("def f():\n    pass\nf(1)").Problem, Is.EqualTo("f() takes 0 positional arguments but 1 was given"));
            Assert.That(Fails("def f(a):\n    pass\nf(z=1)").Problem, Is.EqualTo("f() got an unexpected keyword argument 'z'"));
            Assert.That(Fails("def f(a):\n    pass\nf(1, a=2)").Problem, Is.EqualTo("f() got multiple values for argument 'a'"));
            Assert.That(Fails("def f(a, b):\n    pass\nf(b=1)").Problem, Does.Contain("missing 1 required positional argument: 'a'"));
            Assert.That(Fails("def f(a):\n    pass\nf(1, 2, a=3)").Problem, Does.Contain("positional argument"));
            Assert.That(Fails("len(1, 2)").Problem, Is.EqualTo("len() takes exactly 1 argument (2 given)"));
            Assert.That(Fails("len()").Problem, Is.EqualTo("len() takes exactly 1 argument (0 given)"));
            Assert.That(Fails("len(x=1)").Problem, Is.EqualTo("len() takes no keyword arguments"));
            Assert.That(Fails("[].append()").Problem, Is.EqualTo("list.append() takes exactly 1 argument (0 given)"));
        }

        [Test]
        public void LocalsAreIsolatedFromGlobals()
        {
            Assert.That(Out(@"
x = 1
def f():
    x = 2
    return x
print(f(), x)"), Is.EqualTo("2 1"));
        }

        [Test]
        public void GlobalStatementWritesGlobal()
        {
            Assert.That(Out(@"
count = 0
def bump():
    global count
    count += 1
bump()
bump()
print(count)"), Is.EqualTo("2"));
        }

        [Test]
        public void FunctionsSeeGlobalsDefinedLater()
        {
            Assert.That(Out("def f():\n    return g()\ndef g():\n    return 7\nprint(f())"), Is.EqualTo("7"));
        }

        [Test]
        public void UnboundLocalIsReported()
        {
            var e = Fails("x = 1\ndef f():\n    print(x)\n    x = 2\nf()");
            Assert.That(e.TypeName, Is.EqualTo("UnboundLocalError"));
            Assert.That(e.Problem, Is.EqualTo("local variable 'x' referenced before assignment"));
        }

        [Test]
        public void UndefinedNameIsReported()
        {
            var e = Fails("print(missing)");
            Assert.That(e.TypeName, Is.EqualTo("NameError"));
            Assert.That(e.Problem, Is.EqualTo("name 'missing' is not defined"));
        }

        [Test]
        public void FunctionsAreFirstClass()
        {
            Assert.That(Out("def f():\n    return 1\ng = f\nprint(g(), f is g)"), Is.EqualTo("1 True"));
            Assert.That(Out("def f():\n    pass\nprint(f)"), Is.EqualTo("<function f>"));
            Assert.That(Out("def add(a, b):\n    return a + b\nops = [add]\nprint(ops[0](1, 2))"), Is.EqualTo("3"));
        }

        [Test]
        public void DeepRecursionRaisesRecursionError()
        {
            var e = Fails("def f():\n    return f()\nf()");
            Assert.That(e.TypeName, Is.EqualTo("RecursionError"));
            Assert.That(e.Problem, Does.Contain("maximum recursion depth"));
            Assert.That(e.Trace.Count, Is.GreaterThan(100));
        }

        [Test]
        public void RecursionErrorIsCatchable()
        {
            Assert.That(Out(@"
def f():
    return f()
try:
    f()
except RecursionError:
    print('caught')"), Is.EqualTo("caught"));
        }

        [Test]
        public void CallDepthLimitIsConfigurable()
        {
            var options = new VmOptions { MaxCallDepth = 5 };
            var e = Fails("def f(n):\n    return f(n + 1)\nf(0)", options: options);
            Assert.That(e.Trace.Count, Is.EqualTo(5));
        }

        [Test]
        public void ListMethodsAndMutation()
        {
            Assert.That(Out("xs = [3, 1, 2]\nxs.append(0)\nxs.sort()\nprint(xs)"), Is.EqualTo("[0, 1, 2, 3]"));
            Assert.That(Out("xs = [1, 2]\nxs[0] = 9\nxs[-1] += 5\nprint(xs)"), Is.EqualTo("[9, 7]"));
            Assert.That(Fails("xs = [1, 2, 3]\nxs[1:2] = [9]").Problem, Is.EqualTo("slice assignment is not supported yet"));
        }

        [Test]
        public void DictOperations()
        {
            Assert.That(Out("d = {}\nd['a'] = 1\nd['a'] += 1\nd[2] = 'two'\nprint(d, len(d))"), Is.EqualTo("{'a': 2, 2: 'two'} 2"));
            Assert.That(Out("d = {'a': 1}\nprint(d.get('b', 0), d.get('a'))"), Is.EqualTo("0 1"));
            Assert.That(Out("d = {1: 'a'}\nprint(d[1.0], d[True])"), Is.EqualTo("a a"));
        }

        [Test]
        public void SubscriptAugmentedAssignmentEvaluatesOnce()
        {
            Assert.That(Out(@"
calls = []
def idx():
    calls.append(1)
    return 0
xs = [10]
xs[idx()] += 5
print(xs, len(calls))"), Is.EqualTo("[15] 1"));
        }

        [Test]
        public void StringMethodsWork()
        {
            Assert.That(Out("print('a,b,c'.split(','))"), Is.EqualTo("['a', 'b', 'c']"));
            Assert.That(Out("print('-'.join(['a', 'b']))"), Is.EqualTo("a-b"));
            Assert.That(Out("print('Hello'.upper(), 'Hello'.lower())"), Is.EqualTo("HELLO hello"));
            Assert.That(Out("s = 'abc'\nprint(s.startswith('a'), s.replace('b', 'X'))"), Is.EqualTo("True aXc"));
        }

        [Test]
        public void BoundMethodsCanBeStored()
        {
            Assert.That(Out("xs = []\nadd = xs.append\nadd(1)\nadd(2)\nprint(xs)"), Is.EqualTo("[1, 2]"));
        }

        [Test]
        public void FStrings()
        {
            Assert.That(Out("x = 5\nprint(f'x={x}')"), Is.EqualTo("x=5"));
            Assert.That(Out("print(f'{1 + 2} and {\"a\" * 2}')"), Is.EqualTo("3 and aa"));
            Assert.That(Out("pi = 3.14159\nprint(f'{pi:.2f}|{42:5d}|{42:<5}|{7:03}')"), Is.EqualTo("3.14|   42|42   |007"));
            Assert.That(Out("print(f'{\"a\"!r} {\"b\"!s}')"), Is.EqualTo("'a' b"));
            Assert.That(Out("print(f'{{literal}}')"), Is.EqualTo("{literal}"));
            Assert.That(Out("print(f'')"), Is.EqualTo(""));
        }

        [Test]
        public void PrintSeparatorsAndEnd()
        {
            Assert.That(Run("print(1, 2, 3)"), Is.EqualTo("1 2 3\n"));
            Assert.That(Run("print(1, 2, sep='-', end='!')"), Is.EqualTo("1-2!"));
            Assert.That(Run("print()"), Is.EqualTo("\n"));
        }

        [Test]
        public void IntegerOverflowRaises()
        {
            var e = Fails("x = 9223372036854775807\nx + 1");
            Assert.That(e.TypeName, Is.EqualTo("OverflowError"));
        }

        [Test]
        public void DivisionByZero()
        {
            Assert.That(Fails("1 / 0").TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(Fails("1 // 0").TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(Fails("1 % 0").TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(Fails("1.0 / 0").Problem, Does.Contain("division by zero"));
        }

        [Test]
        public void TypeErrorsOnBadOperands()
        {
            Assert.That(Fails("1 + 'a'").Problem, Is.EqualTo("unsupported operand type(s) for +: 'int' and 'str'"));
            Assert.That(Fails("1 < 'a'").Problem, Is.EqualTo("'<' not supported between instances of 'int' and 'str'"));
            Assert.That(Fails("5()").Problem, Is.EqualTo("'int' object is not callable"));
            Assert.That(Fails("None.x").TypeName, Is.EqualTo("AttributeError"));
            Assert.That(Fails("for x in 5:\n    pass").Problem, Is.EqualTo("'int' object is not iterable"));
            Assert.That(Fails("{[1]: 2}").Problem, Is.EqualTo("unhashable type: 'list'"));
            Assert.That(Fails("[1][5]").TypeName, Is.EqualTo("IndexError"));
            Assert.That(Fails("{}['k']").TypeName, Is.EqualTo("KeyError"));
        }

        [Test]
        public void AttributeAssignmentIsRejected()
        {
            Assert.That(Fails("x = 1\nx.y = 2").TypeName, Is.EqualTo("AttributeError"));
            Assert.That(Fails("import math\nmath.pi = 3").Problem, Is.EqualTo("module attributes are read-only"));
        }

        [Test]
        public void ImportsWork()
        {
            Assert.That(Out("import math\nprint(math.sqrt(16))"), Is.EqualTo("4.0"));
            Assert.That(Out("import math as m\nprint(m.floor(2.7))"), Is.EqualTo("2"));
            Assert.That(Out("from math import sqrt, pi as p\nprint(sqrt(9), round(p, 2))"), Is.EqualTo("3.0 3.14"));
            Assert.That(Out("import math\nprint(math)"), Is.EqualTo("<module 'math'>"));
        }

        [Test]
        public void ImportErrors()
        {
            Assert.That(Fails("import nothing").Problem, Is.EqualTo("No module named 'nothing'"));
            Assert.That(Fails("from math import nothing").Problem, Is.EqualTo("cannot import name 'nothing' from 'math'"));
            Assert.That(Fails("import math\nmath.nothing").TypeName, Is.EqualTo("AttributeError"));
        }

        [Test]
        public void ImportInsideFunctionBindsLocally()
        {
            Assert.That(Out("def f():\n    import math\n    return math.sqrt(4)\nprint(f())"), Is.EqualTo("2.0"));
        }

        [Test]
        public void BuiltinFunctionsFromScripts()
        {
            Assert.That(Out("print(len('abc'), abs(-2), min(3, 1, 2), max([4, 9]), sum([1, 2, 3]))"), Is.EqualTo("3 2 1 9 6"));
            Assert.That(Out("print(sorted([3, 1, 2], reverse=True))"), Is.EqualTo("[3, 2, 1]"));
            Assert.That(Out("print(list(enumerate('ab')), list(zip([1, 2], 'xy')))"), Is.EqualTo("[(0, 'a'), (1, 'b')] [(1, 'x'), (2, 'y')]"));
            Assert.That(Out("print(int('12') + 1, float('1.5'), str(3), bool(0), repr('a'))"), Is.EqualTo("13 1.5 3 False 'a'"));
            Assert.That(Out("print(list(range(3)), tuple([1]), dict([(1, 2)]))"), Is.EqualTo("[0, 1, 2] (1,) {1: 2}"));
        }

        [Test]
        public void ErrorsReportLineNumbers()
        {
            var e = Fails("x = 1\ny = 2\nz = x / 0\n");
            Assert.That(e.Line, Is.EqualTo(3));
            Assert.That(e.TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(e.Message, Does.Contain("line 3"));
        }

        [Test]
        public void TracebackListsCallers()
        {
            var e = Fails("def inner():\n    return 1 / 0\ndef outer():\n    return inner()\nouter()");
            Assert.That(e.Trace.Count, Is.EqualTo(3));
            Assert.That(e.Trace[0].Function, Is.EqualTo("inner"));
            Assert.That(e.Trace[0].Line, Is.EqualTo(2));
            Assert.That(e.Trace[1].Function, Is.EqualTo("outer"));
            Assert.That(e.Trace[1].Line, Is.EqualTo(4));
            Assert.That(e.Trace[2].Function, Is.EqualTo("<module>"));
            Assert.That(e.Trace[2].Line, Is.EqualTo(5));
            string tb = e.FormatTraceback();
            Assert.That(tb, Does.StartWith("Traceback (most recent call last):"));
            Assert.That(tb, Does.EndWith("ZeroDivisionError: division by zero"));
            Assert.That(tb.IndexOf("in <module>"), Is.LessThan(tb.IndexOf("in inner")));
        }

        [Test]
        public void SyntaxErrorsAreThrownByCompile()
        {
            var vm = new PyVm();
            var e = Assert.Throws<PySyntaxError>(() => vm.Compile("x = = 1"));
            Assert.That(e.Line, Is.EqualTo(1));
        }
    }
}
