using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpPython.Tests.Vm
{
    public class SnapshotTests
    {
        // A VM with one instruction per slice, so a snapshot can be taken between any two instructions.
        private static PyVm NewVm(StringBuilder sb, bool snapshots = true, int slice = 1, long maxSteps = 0)
        {
            var vm = new PyVm(new VmOptions { EnableSnapshots = snapshots, StepsPerSlice = slice, MaxSteps = maxSteps })
            {
                Output = s => sb.Append(s)
            };
            vm.RegisterModule("game", m => m
                .Native("wait", (ctx, a) => { ctx.Wait((int)a[0].AsInt()); return Value.None; }, 1, 1)
                .Native("fetch", (ctx, a) => { ctx.Wait((int)a[0].AsInt(), a[1]); return Value.None; }, 2, 2)
                .Function("add", (x, y) => x.AsInt() + y.AsInt())
                .Function("player", () => Value.FromHandle(7, 42))
                .Constant("MAX", 10));
            return vm;
        }

        private static string Describe(PyTask task) =>
            task.State + (task.Error != null ? " " + task.Error.Message : "");

        private const int TickLimit = 100000;

        private static string Reference(string source, Action<PyTask, int> hook)
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb);
            var task = vm.Start(source);
            for (int tick = 0; tick < TickLimit; tick++)
            {
                hook?.Invoke(task, tick);
                var state = task.Update();
                if (state == TaskState.Error || state == TaskState.Finished && task.PendingEvents == 0) break;
            }
            return sb + "|" + Describe(task);
        }

        // Saves and restores into a brand new VM after every single instruction.
        private static string Chained(string source, Action<PyTask, int> hook)
        {
            var sb = new StringBuilder();
            var script = NewVm(sb).Compile(source);
            var task = NewVm(sb).Start(script);
            for (int tick = 0; tick < TickLimit; tick++)
            {
                hook?.Invoke(task, tick);
                var state = task.Update();
                if (state == TaskState.Error || state == TaskState.Finished && task.PendingEvents == 0) break;
                byte[] bytes = task.Save();
                var restored = NewVm(sb).Restore(script, bytes);
                Assert.That(restored.Save(), Is.EqualTo(bytes), "saving a restored task must give identical bytes (tick " + tick + ")");
                task = restored;
            }
            return sb + "|" + Describe(task);
        }

        private static void RoundTrips(string source, Action<PyTask, int> hook = null)
        {
            string expected = Reference(source, hook);
            Assert.That(Chained(source, hook), Is.EqualTo(expected));
            Assert.That(expected, Does.Not.Contain("Error 'HostError'"));
        }

        private static readonly string[] Scripts =
        {
            "x = 1\nprint(x + 2)",
            "total = 0\nfor i in range(6):\n    total += i * i\nprint(total)",
            "xs = [3, 1, 2]\nfor x in xs:\n    print(x)\nprint(sorted(xs))",
            "for c in 'abc':\n    print(c, ord(c))",
            "for t in (1, 'two', 3.5):\n    print(t)",
            "d = {'a': 1, 'b': 2, 'c': 3}\nfor k in d:\n    print(k, d[k])",
            "for i, c in enumerate('xyz', 1):\n    print(i, c)",
            "for a, b in zip([1, 2, 3], 'abc'):\n    print(a, b)",
            "for x in reversed([1, 2, 3]):\n    print(x)",
            "for x in range(10, 0, -3):\n    print(x)",
            "def f(a, b=2):\n    return a * b\nprint(f(3), f(3, 4), f(b=5, a=1))",
            "def fact(n):\n    if n <= 1:\n        return 1\n    return n * fact(n - 1)\nprint(fact(10))",
            "def fib(n):\n    return n if n < 2 else fib(n - 1) + fib(n - 2)\nprint(fib(9))",
            "xs = []\nadd = xs.append\nfor i in range(4):\n    add(i)\nprint(xs)",
            "xs = [1]\nys = xs\nys.append(2)\nprint(xs is ys, xs, len(xs))",
            "xs = []\nxs.append(xs)\nprint(xs[0] is xs, len(xs[0]))",
            "d = {(1, 2): 'a', (3, 4): 'b'}\nprint(d[(1, 2)], d[(3, 4)], (1, 2) in d)",
            "d = {}\nfor w in 'hello world'.split():\n    d[w] = len(w)\nprint(d)",
            "try:\n    x = 1 / 0\nexcept ZeroDivisionError as e:\n    print('caught', e)\nprint('after')",
            "def g():\n    raise ValueError('boom')\ntry:\n    g()\nexcept ValueError as e:\n    print(e)\n    try:\n        raise\n    except ValueError:\n        print('again')",
            "for i in range(3):\n    try:\n        if i == 1:\n            raise KeyError('k')\n        print('ok', i)\n    except KeyError:\n        print('handled', i)",
            "d = {'a': 1}\ntry:\n    for k in d:\n        d[k + 'x'] = 1\nexcept RuntimeError as e:\n    print(e)",
            "e = ValueError('kept')\ntry:\n    raise e\nexcept Exception as caught:\n    print(caught is e, caught)",
            "s = 0\nfor i in range(3):\n    for j in range(3):\n        if j == 2:\n            break\n        s += i * 10 + j\nprint(s)",
            "msg = f'{1 + 2:03d} and {\"x\".upper()}'\nprint(msg)",
            "xs = [5, 3, 9]\nxs.sort()\nprint(xs[::-1], xs[1:], min(xs), max(xs), sum(xs))",
            "a, (b, c) = 1, (2, 3)\nprint(a, b, c)",
            "import game\nprint(game.add(2, 3), game.MAX)",
            "import game\nh = game.player()\nprint(h == game.player(), h)",
            "import math\nprint(math.sqrt(16), math.floor(2.5))",
            "from game import add\nprint(add(1, 1))",
            "import game\nprint('a')\ngame.wait(3)\nprint('b')\ngame.wait(0)\nprint('c')",
            "import game\nx = game.fetch(2, 'result')\nprint(x)",
            "import game\nfor i in range(3):\n    game.wait(2)\n    print('tick', i)",
            "r = range(5)\nit = iter if False else None\nprint(r[2], len(r), list(r))",
            "n = None\nb = True\nf = 2.5e-3\nbig = 2 ** 62\nprint(n, b, f, big, -big)",
            "s = 'naïve ✓'\nprint(s, len(s))",
            "def counter():\n    return [0]\nc = counter()\nc[0] += 1\nprint(c)",
            "def make():\n    n = 0\n    def inc():\n        nonlocal n\n        n += 1\n        return n\n    return inc\nc = make()\nd = make()\nc()\nc()\nprint(c(), d())",
            "import game\ndef make():\n    n = 0\n    def inc():\n        nonlocal n\n        game.wait(1)\n        n += 1\n        return n\n    return inc\nc = make()\nprint(c(), c(), c())",
            "def outer(n):\n    def fact(k):\n        return 1 if k <= 1 else k * fact(k - 1)\n    return fact(n)\nprint(outer(8))",
            "def make():\n    xs = []\n    def add(v):\n        xs.append(v)\n        return xs\n    def get():\n        return xs\n    return add, get\nadd, get = make()\nadd(1)\nadd(2)\nprint(get(), get() is get())",
            "def a():\n    x = 1\n    def b():\n        def c():\n            nonlocal x\n            x += 10\n            return x\n        return c\n    return b()\nf = a()\nprint(f(), f())",
            "fs = []\nfor i in range(3):\n    fs.append(lambda i=i: i * 2)\nprint(fs[0](), fs[1](), fs[2]())",
            "print(sorted([3, -5, 1], key=lambda v: abs(v)))",
            "t = type(1)\nd = {t: 'i', str: 's'}\nprint(d[int], t('5') + 1, isinstance(True, (str, int)), type(ValueError('x')))",
            "import game\nt = int\ngame.wait(1)\nprint(t('4'), callable(t), issubclass(bool, t))",
            "def f(a, b=2, *args, c, d=4, **kw):\n    return a, b, args, c, d, kw\nprint(f(1, 9, 8, 7, c=3, z=1))",
            "import game\ndef f(*xs, **opts):\n    game.wait(1)\n    return xs, opts\nprint(f(*[1, 2], **{'k': 3}))",
            "def g(*a, **k):\n    return a, k\ndef f(*a, **k):\n    return g(*a, **k)\nprint(f(1, x=2), f())",
            "print([x * x for x in range(6) if x % 2], {k: k + 1 for k in range(3)}, sum(x for x in range(5)))",
            "import game\nprint([game.add(x, 1) for x in range(3)])",
            "import game\nprint([(game.wait(1), x)[1] for x in range(3)])",
            "def f(n):\n    return [[i * j for j in range(n)] for i in range(n)]\nprint(f(3))",
            "def f():\n    cell = []\n    def g():\n        return cell\n    cell.append(g)\n    return g\nh = f()\nprint(h()[0] is h)",
            "def f():\n    try:\n        n = 0\n        def g():\n            nonlocal n\n            n += 1\n        g()\n        raise ValueError('x')\n    except ValueError:\n        g()\n    finally:\n        g()\n    return n\nprint(f())",
            "class A:\n    n = 5\n    def __init__(self, x):\n        self.x = x\n    def get(self):\n        return self.x + A.n\na = A(3)\nprint(a.get(), a.x, isinstance(a, A), type(a) is A)",
            "class A:\n    def f(self):\n        return 'A'\nclass B(A):\n    def g(self):\n        return self.f() + 'B'\nb = B()\nprint(b.g(), isinstance(b, A), issubclass(B, A), B.__name__)",
            "class N:\n    def __init__(self, v):\n        self.v = v\n        self.other = None\na = N(1)\nb = N(2)\na.other = b\nb.other = a\nprint(a.other.other is a, b.other.v)",
            "import game\nclass A:\n    def __init__(self):\n        game.wait(1)\n        self.x = 5\n        game.wait(1)\n    def get(self):\n        game.wait(1)\n        return self.x\na = A()\nprint(a.x, a.get())",
            "import game\nclass A:\n    k = 2\n    def __init__(self, v):\n        self.v = v\nxs = [A(i) for i in range(3)]\ngame.wait(2)\nprint([x.v * A.k for x in xs])",
            "class A:\n    def __init__(self, k):\n        self.k = k\n    def scale(self, x):\n        return x * self.k\nm = A(3)\nf = m.scale\nprint(f(2), sorted([3, 1, 2], key=m.scale))",
            "def make(n):\n    class L:\n        def get(self):\n            return n\n    return L\nK = make(4)\nprint(K().get(), make(5)().get())",
            "import game\nclass A:\n    pass\na = A()\na.f = lambda x: x + 1\nd = {A: 1, a: 2}\ngame.wait(1)\nprint(a.f(1), d[A], d[a], A in d)",
            "import game\nclass A:\n    items = []\n    def add(self, x):\n        A.items.append(x)\n        game.wait(1)\na = A()\na.add(1)\na.add(2)\nprint(A.items, a.items is A.items)",
            "class Outer:\n    class Inner:\n        v = 3\n    def make(self):\n        return Outer.Inner()\nprint(Outer().make().v)",
            "class A:\n    def __init__(self):\n        return None\nprint(type(A()).__name__)",
            "import game\nclass A:\n    def __init__(self, x):\n        game.wait(1)\n        self.x = x\nclass B(A):\n    def __init__(self, x):\n        super().__init__(x + 1)\n        game.wait(1)\n        self.y = x\nclass C(B):\n    def __init__(self):\n        super(C, self).__init__(10)\nc = C()\nprint(c.x, c.y)",
            "import game\nclass A:\n    def f(self, n):\n        return n * 2\nclass B(A):\n    def f(self, n):\n        return super().f(game.wait(1) or n) + 1\nprint(B().f(4))",
            "import game\nclass A:\n    def f(self):\n        return 'A'\nclass B(A):\n    def f(self):\n        return 'B' + super().f()\nm = B().f\nunbound = A.f\ngame.wait(1)\nprint(m(), unbound(B()))",
            "class P:\n    def __init__(self, x):\n        self.x = x\n    def __str__(self):\n        return 'P' + str(self.x)\n    def __repr__(self):\n        return '<' + str(self.x) + '>'\nps = [P(1), P(2)]\nprint(ps, str(ps[0]), ps[1])",
            "import game\nclass P:\n    def __str__(self):\n        return 'p'\np = P()\ngame.wait(1)\nprint(p, len([p]))\ngame.wait(1)\nprint(f'{p}')",
        };

        [TestCaseSource(nameof(Scripts))]
        public void ScriptsSurviveASnapshotAfterEveryInstruction(string source) => RoundTrips(source);

        [Test]
        public void StateSurvivesLoadingTwiceFromTheSameBytes()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 5);
            var script = vm.Compile("import game\nxs = [1, 2]\ngame.wait(2)\nxs.append(3)\nprint(xs)");
            var task = vm.Start(script);
            task.Update();
            byte[] bytes = task.Save();
            var a = vm.Restore(script, bytes);
            var b = vm.Restore(script, bytes);
            a.RunUntilDone();
            b.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("[1, 2, 3]\n[1, 2, 3]\n"));
            Assert.That(a.Globals["xs"].AsList(), Is.Not.SameAs(b.Globals["xs"].AsList()));
        }

        [Test]
        public void EventsAreHandledAfterRestore()
        {
            string src = "import game\nhp = 100\ndef on_hit(n):\n    global hp\n    hp -= n\n    print('hp', hp)\nwhile True:\n    game.wait(3)";
            RoundTrips(src, (t, i) =>
            {
                if (i == 4) t.QueueEvent("on_hit", Value.FromInt(10));
                if (i == 9) t.QueueEvent("on_hit", Value.FromInt(5));
                if (i == 12) t.QueueEvent("missing");
                if (i == 60) t.Run();
            });
        }

        [Test]
        public void EventsQueuedBeforeSavingAreKept()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 1000);
            var script = vm.Compile("import game\ndef h(a, b):\n    print(a, b)\ngame.wait(5)");
            var task = vm.Start(script);
            task.Update();
            // A handler interrupts the wait; save while the event is still only queued.
            task.QueueEvent("h", Value.FromInt(1), Value.FromString("x"));
            task.QueueEvent("h", Value.FromInt(2), Value.FromList(new PyList(new[] { Value.FromInt(3) })));
            Assert.That(task.PendingEvents, Is.EqualTo(2));
            var restored = NewVm(sb, slice: 1000).Restore(script, task.Save());
            Assert.That(restored.PendingEvents, Is.EqualTo(2));
            restored.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("1 x\n2 [3]\n"));
        }

        [Test]
        public void AFinishedTaskKeepsItsGlobalsAndHandlesEvents()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 1000);
            var script = vm.Compile("count = 0\ndef bump():\n    global count\n    count += 1\n    print(count)");
            var task = vm.Start(script);
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Finished));
            var restored = vm.Restore(script, task.Save());
            Assert.That(restored.State, Is.EqualTo(TaskState.Finished));
            restored.QueueEvent("bump");
            restored.QueueEvent("bump");
            restored.RunUntilDone();
            Assert.That(sb.ToString(), Is.EqualTo("1\n2\n"));
            Assert.That(restored.Globals["count"].AsInt(), Is.EqualTo(2));
        }

        [Test]
        public void AFailedTaskKeepsItsError()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 1000);
            var script = vm.Compile("def f():\n    return 1 // 0\nx = 5\nf()");
            var task = vm.Start(script);
            Assert.That(task.RunUntilDone(), Is.EqualTo(TaskState.Error));
            var restored = vm.Restore(script, task.Save());
            Assert.That(restored.State, Is.EqualTo(TaskState.Error));
            Assert.That(restored.Error.TypeName, Is.EqualTo("ZeroDivisionError"));
            Assert.That(restored.Error.Line, Is.EqualTo(2));
            Assert.That(restored.Error.FormatTraceback(), Is.EqualTo(task.Error.FormatTraceback()));
            Assert.That(restored.Globals["x"].AsInt(), Is.EqualTo(5));
            Assert.That(restored.QueueEvent("f"), Is.False);
        }

        [Test]
        public void HandlesAndOtherHostValuesAreSavedAsPlainData()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 1000);
            var script = vm.Compile("import game\nhero = game.player()\nitems = [hero, (hero, 1)]\ngame.wait(1)");
            var task = vm.Start(script);
            task.Update();
            var restored = vm.Restore(script, task.Save());
            var hero = restored.Globals["hero"];
            Assert.That(hero.IsHandle, Is.True);
            Assert.That(hero.HandleType, Is.EqualTo(7));
            Assert.That(hero.HandleId, Is.EqualTo(42));
            Assert.That(restored.Globals["items"].AsList().Items[1].AsTuple().Items[0], Is.EqualTo(hero));
        }

        [Test]
        public void TaskOwnerIsNotSavedAndTheHostReattachesIt()
        {
            var vm = NewVm(new StringBuilder(), slice: 1000);
            var script = vm.Compile("x = 1");
            var task = vm.Start(script);
            task.Owner = "player";
            var restored = vm.Restore(script, task.Save());
            Assert.That(restored.Owner, Is.Null);
            Assert.That(restored.Vm, Is.SameAs(vm));
        }

        [Test]
        public void StepCountsAndWaitsAreKept()
        {
            var vm = NewVm(new StringBuilder(), slice: 1000);
            var script = vm.Compile("import game\ngame.wait(7)");
            var task = vm.Start(script);
            task.Update();
            task.Update();
            var restored = vm.Restore(script, task.Save());
            Assert.That(restored.State, Is.EqualTo(TaskState.Waiting));
            Assert.That(restored.WaitTicksRemaining, Is.EqualTo(task.WaitTicksRemaining));
            Assert.That(restored.StepsExecuted, Is.EqualTo(task.StepsExecuted));
            Assert.That(restored.CallDepth, Is.EqualTo(task.CallDepth));
        }

        // ---- Refusals -------------------------------------------------------------------------------------------

        [Test]
        public void SnapshotsAreOffByDefault()
        {
            var vm = new PyVm();
            var script = vm.Compile("x = 1");
            var task = vm.Start(script);
            Assert.That(new VmOptions().EnableSnapshots, Is.False);
            Assert.Throws<SnapshotException>(() => task.Save());
            Assert.Throws<SnapshotException>(() => vm.Restore(script, new byte[10]));
        }

        [Test]
        public void ARestoreAgainstADifferentScriptIsRefused()
        {
            var vm = NewVm(new StringBuilder(), slice: 1000);
            var a = vm.Compile("x = 1\nprint(x)");
            var b = vm.Compile("x = 2\nprint(x)");
            Assert.That(a.Fingerprint, Is.Not.EqualTo(b.Fingerprint));
            var ex = Assert.Throws<SnapshotException>(() => vm.Restore(b, vm.Start(a).Save()));
            Assert.That(ex.Message, Does.Contain("different script"));
        }

        [Test]
        public void TheSameSourceHasTheSameFingerprint()
        {
            var vm = NewVm(new StringBuilder());
            const string src = "def f(a=1):\n    return a\nprint(f())";
            Assert.That(vm.Compile(src).Fingerprint, Is.EqualTo(new PyVm().Compile(src).Fingerprint));
        }

        [Test]
        public void ABadHeaderIsRefused()
        {
            var vm = NewVm(new StringBuilder(), slice: 1000);
            var script = vm.Compile("x = 1");
            byte[] good = vm.Start(script).Save();

            Assert.That(Assert.Throws<SnapshotException>(() => vm.Restore(script, new byte[0])).Message, Does.Contain("damaged").Or.Contain("not a"));
            var wrongMagic = (byte[])good.Clone();
            wrongMagic[0] ^= 0xFF;
            Assert.That(Assert.Throws<SnapshotException>(() => vm.Restore(script, wrongMagic)).Message, Does.Contain("not a CSharpPython snapshot"));
            var wrongVersion = (byte[])good.Clone();
            wrongVersion[4] = 99;
            Assert.That(Assert.Throws<SnapshotException>(() => vm.Restore(script, wrongVersion)).Message, Does.Contain("version"));
            var trailing = new byte[good.Length + 1];
            Array.Copy(good, trailing, good.Length);
            Assert.Throws<SnapshotException>(() => vm.Restore(script, trailing));
            Assert.Throws<ArgumentNullException>(() => vm.Restore(null, good));
            Assert.Throws<ArgumentNullException>(() => vm.Restore(script, null));
        }

        [Test]
        public void AHostFunctionThatIsNotRegisteredCannotBeSaved()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 1000);
            vm.RegisterModule("loose", m => m.Function("make", () =>
                Value.FromNative(new NativeFunction("secret", (ctx, a) => Value.None, 0, 0))));
            var script = vm.Compile("import loose\nf = loose.make()");
            var task = vm.Start(script);
            task.RunUntilDone();
            var ex = Assert.Throws<SnapshotException>(() => task.Save());
            Assert.That(ex.Message, Does.Contain("secret").And.Contain("registered"));
        }

        [Test]
        public void RestoreNeedsTheHostApiToBeRegisteredAgain()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 1000);
            var script = vm.Compile("import game\nadd = game.add\nm = game\nx = 1");
            var task = vm.Start(script);
            task.RunUntilDone();
            byte[] bytes = task.Save();

            var bare = new PyVm(new VmOptions { EnableSnapshots = true });
            Assert.That(Assert.Throws<SnapshotException>(() => bare.Restore(script, bytes)).Message, Does.Contain("game"));

            var other = new PyVm(new VmOptions { EnableSnapshots = true });
            other.RegisterModule("game", m => m.Constant("MAX", 1));
            Assert.Throws<SnapshotException>(() => other.Restore(script, bytes));
        }

        [Test]
        public void BuiltinsMethodsAndModulesAreSavedByName()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 1000);
            vm.SetBuiltin("twice", Value.FromNative(new NativeFunction("twice", (ctx, a) => a[0].AsInt() * 2, 1, 1)));
            var script = vm.Compile("import math\nf = len\ng = twice\nm = math\nh = math.sqrt\nu = 'x'.upper\nv = [].append\nprint(g(4), h(9), u())");
            var task = vm.Start(script);
            task.RunUntilDone();
            var vm2 = NewVm(sb, slice: 1000);
            vm2.SetBuiltin("twice", Value.FromNative(new NativeFunction("twice", (ctx, a) => a[0].AsInt() * 2, 1, 1)));
            var restored = vm2.Restore(script, task.Save());
            Assert.That(restored.Globals["f"].Kind, Is.EqualTo(ValueKind.Native));
            Assert.That(restored.Globals["m"].Kind, Is.EqualTo(ValueKind.Module));
            Assert.That(restored.Globals["u"].Kind, Is.EqualTo(ValueKind.BoundMethod));
            Assert.That(restored.Save(), Is.EqualTo(task.Save()));
        }

        [Test]
        public void CustomExceptionTypesAreSavedByName()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 1);
            vm.RegisterExceptionType("SnapGameError");
            vm.RegisterModule("g", m => m.Native("fail", (c, a) => throw new PyError("SnapGameError", "bad"), 0, 0));
            var script = vm.Compile("import g\nE = SnapGameError\ntry:\n    g.fail()\nexcept SnapGameError as e:\n    print('got', e)\nprint(E)");
            var task = vm.Start(script);
            for (int i = 0; i < 1000 && task.State != TaskState.Finished; i++)
            {
                task.Update();
                task = vm.Restore(script, task.Save());
            }
            Assert.That(sb.ToString(), Is.EqualTo("got SnapGameError: bad\n<class 'SnapGameError'>\n").Or.EqualTo("got bad\n<class 'SnapGameError'>\n"));
        }

        // ---- Damaged data ---------------------------------------------------------------------------------------

        private static byte[] RichSnapshot(out Script script, out PyVm vm)
        {
            var sb = new StringBuilder();
            vm = NewVm(sb, slice: 40);
            script = vm.Compile(
                "import game\n" +
                "def f(a, b=[1, 2]):\n    return a\n" +
                "xs = [1, 'two', 3.0, None, (4, 5), {'k': [6]}]\n" +
                "ys = xs\nxs.append(xs)\nm = xs.append\nit = iter(xs)\n" +
                "d = {(1, 2): 'v', 'z': f}\n" +
                "for k in d:\n    game.wait(2)\n    print(k)\n");
            var task = vm.Start(script);
            task.Update();
            task.Update();
            task.QueueEvent("f", Value.FromInt(1));
            return task.Save();
        }

        [Test]
        public void EveryTruncationIsReportedAsASnapshotError()
        {
            byte[] good = RichSnapshot(out var script, out var vm);
            for (int length = 0; length < good.Length; length++)
            {
                var cut = new byte[length];
                Array.Copy(good, cut, length);
                Assert.Throws<SnapshotException>(() => vm.Restore(script, cut), "length " + length);
            }
        }

        [Test]
        public void FlippedBytesNeverCrashTheHost()
        {
            byte[] good = RichSnapshot(out var script, out var vm);
            var random = new Random(1234);
            int restored = 0, refused = 0;
            for (int attempt = 0; attempt < 3000; attempt++)
            {
                var data = (byte[])good.Clone();
                int flips = 1 + random.Next(3);
                for (int i = 0; i < flips; i++) data[random.Next(data.Length)] = (byte)random.Next(256);
                var damagedVm = NewVm(new StringBuilder(), slice: 50, maxSteps: 2000);
                PyTask task;
                try
                {
                    task = damagedVm.Restore(script, data);
                }
                catch (SnapshotException)
                {
                    refused++;
                    continue;
                }
                restored++;
                // Whatever came back must be safe to run and to save again.
                for (int i = 0; i < 50 && task.State != TaskState.Error; i++) task.Update();
                try { task.Save(); } catch (SnapshotException) { }
            }
            Assert.That(refused, Is.GreaterThan(0));
            Assert.That(restored + refused, Is.EqualTo(3000));
        }

        [Test]
        public void ADamagedFrameIsRefused()
        {
            var vm = NewVm(new StringBuilder(), slice: 1000);
            var script = vm.Compile("x = 1");
            var task = vm.Start(script);
            byte[] good = task.Save();
            // The frame record is at the end: code index, pc, base, callee, isEvent, handlerCount, excInfo(None).
            int pcOffset = good.Length - (4 + 4 + 4 + 4 + 1 + 4 + 1) + 4;
            var bad = (byte[])good.Clone();
            bad[pcOffset + 3] = 0x7F;
            Assert.Throws<SnapshotException>(() => vm.Restore(script, bad));
        }

        [Test]
        public void ARecursiveCallDepthBeyondTheLimitIsRefused()
        {
            var script = new PyVm().Compile("def f(n):\n    return f(n + 1)\nf(0)");
            var sb = new StringBuilder();
            var deep = new PyVm(new VmOptions { EnableSnapshots = true, StepsPerSlice = 400, MaxCallDepth = 150 });
            var task = deep.Start(script);
            task.Update();
            byte[] bytes = task.Save();
            var shallow = new PyVm(new VmOptions { EnableSnapshots = true, MaxCallDepth = 5 });
            Assert.That(Assert.Throws<SnapshotException>(() => shallow.Restore(script, bytes)).Message, Does.Contain("MaxCallDepth"));
        }

        // ---- FastForward ----------------------------------------------------------------------------------------

        private static string RunTicks(string source, int ticks, bool fast, Action<PyTask> afterEach = null)
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 7);
            var task = vm.Start(source);
            if (fast) task.FastForward(ticks);
            else for (int i = 0; i < ticks; i++) task.Update();
            return sb + "|" + task.State + "|" + task.WaitTicksRemaining + "|" + task.StepsExecuted;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(11)]
        [TestCase(40)]
        [TestCase(500)]
        public void FastForwardMatchesTickByTick(int ticks)
        {
            const string src = "import game\nprint('start')\nfor i in range(4):\n    game.wait(9)\n    print('lap', i)\ngame.wait(30)\nprint('end')";
            Assert.That(RunTicks(src, ticks, true), Is.EqualTo(RunTicks(src, ticks, false)));
        }

        [Test]
        public void FastForwardHandlesEventsAndStops()
        {
            var sbA = new StringBuilder();
            var sbB = new StringBuilder();
            const string src = "import game\ndef h():\n    print('event')\ngame.wait(1000)\nprint('done')";
            var a = NewVm(sbA, slice: 50).Start(src);
            var b = NewVm(sbB, slice: 50).Start(src);
            a.Update();
            b.Update();
            a.QueueEvent("h");
            b.QueueEvent("h");
            Assert.That(a.FastForward(2000), Is.EqualTo(TaskState.Finished));
            for (int i = 0; i < 2000; i++) b.Update();
            Assert.That(sbA.ToString(), Is.EqualTo(sbB.ToString()));
            Assert.That(sbA.ToString(), Is.EqualTo("event\ndone\n"));
            Assert.That(a.FastForward(10), Is.EqualTo(TaskState.Finished));

            var failing = NewVm(new StringBuilder()).Start("x = 1 // 0");
            Assert.That(failing.FastForward(5), Is.EqualTo(TaskState.Error));
        }

        [Test]
        public void FastForwardIsCheapForLongWaits()
        {
            var vm = NewVm(new StringBuilder(), slice: 1000);
            var task = vm.Start("import game\ngame.wait(2000000000)\nprint('x')");
            task.Update();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            task.FastForward(1999999990);
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(1000));
            Assert.That(task.State, Is.EqualTo(TaskState.Waiting));
            Assert.That(task.WaitTicksRemaining, Is.EqualTo(10));
        }

        [Test]
        public void ARestoredTaskCanBeFastForwardedToCatchUpWithOfflineTime()
        {
            var sb = new StringBuilder();
            var vm = NewVm(sb, slice: 1000);
            var script = vm.Compile("import game\nproduced = 0\nwhile produced < 5:\n    game.wait(100)\n    produced += 1\nprint('produced', produced)");
            var task = vm.Start(script);
            task.Update();
            var restored = vm.Restore(script, task.Save());
            restored.FastForward(1000);
            Assert.That(restored.State, Is.EqualTo(TaskState.Finished));
            Assert.That(sb.ToString(), Is.EqualTo("produced 5\n"));
        }
    }
}
