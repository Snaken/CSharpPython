using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Vm
{
    public class MapFilterTests
    {
        [Test]
        public void MapAppliesTheFunctionLazily()
        {
            Assert.That(Out("def f(x):\n    print('call', x)\n    return x * 2\nm = map(f, [1, 2, 3])\nprint('made')\nprint(next(m))\nprint(list(m))"),
                Is.EqualTo("made\ncall 1\n2\ncall 2\ncall 3\n[4, 6]"));
        }

        [Test]
        public void MapTakesSeveralIterablesAndStopsAtTheShortest()
        {
            Assert.That(Out("print(list(map(lambda a, b: a + b, [1, 2, 3], (10, 20))))"), Is.EqualTo("[11, 22]"));
            Assert.That(Out("print(list(map(str, range(3))))"), Is.EqualTo("['0', '1', '2']"));
        }

        [Test]
        public void MapWorksOnInfiniteGenerators()
        {
            Assert.That(Out("def n():\n    i = 0\n    while True:\n        yield i\n        i += 1\nfor v in map(lambda x: x * x, n()):\n    if v > 10:\n        break\n    print(v)"),
                Is.EqualTo("0\n1\n4\n9"));
        }

        [Test]
        public void FilterKeepsTruthyResults()
        {
            Assert.That(Out("print(list(filter(lambda x: x % 2, range(7))))"), Is.EqualTo("[1, 3, 5]"));
            Assert.That(Out("print(list(filter(None, [0, 1, '', 'a', [], [2]])))"), Is.EqualTo("[1, 'a', [2]]"));
        }

        [Test]
        public void FilterIsLazy()
        {
            Assert.That(Out("def p(x):\n    print('test', x)\n    return x > 1\nf = filter(p, [1, 2, 3])\nprint(next(f))\nprint(next(f))"),
                Is.EqualTo("test 1\ntest 2\n2\ntest 3\n3"));
        }

        [Test]
        public void ErrorsInTheFunctionPropagate()
        {
            Assert.That(Fails("list(map(lambda x: 1 // x, [1, 0]))").TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(Fails("map(len)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("map(len, 5)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("filter(None)").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void MapAndFilterCompose()
        {
            Assert.That(Out("print(sum(map(lambda x: x * 2, filter(lambda x: x > 2, range(6)))))"), Is.EqualTo("24"));
        }

        [Test]
        public void RadixFunctionsMatchPython()
        {
            Assert.That(Out("print(bin(5), bin(-5), bin(0), oct(8), oct(-9), hex(255), hex(-255), hex(0), hex(True))"),
                Is.EqualTo("0b101 -0b101 0b0 0o10 -0o11 0xff -0xff 0x0 0x1"));
            Assert.That(Fails("hex(1.5)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("bin('a')").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("hex(-9223372036854775807 - 1)").TypeName, Is.EqualTo("ValueError"));
        }

        [Test]
        public void IdIsStablePerObject()
        {
            Assert.That(Out("a = [1]\nb = [1]\nprint(id(a) == id(a), id(a) == id(b), id(5) == id(5))"), Is.EqualTo("True False True"));
        }

        [Test]
        public void DelattrRemovesAnAttribute()
        {
            Assert.That(Out("class A:\n    pass\na = A()\na.x = 1\ndelattr(a, 'x')\nprint(hasattr(a, 'x'))"), Is.EqualTo("False"));
            Assert.That(Fails("class A:\n    pass\ndelattr(A(), 'x')").TypeName, Is.EqualTo("AttributeError"));
            Assert.That(Fails("delattr(1, 2)").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void AsciiEscapesNonAsciiCharacters()
        {
            Assert.That(Out("print(ascii('abc'), ascii('é'), ascii('€'), ascii('😀'), ascii(['é']))"),
                Is.EqualTo("'abc' '\\xe9' '\\u20ac' '\\U0001f600' ['\\xe9']"));
        }
    }
}
