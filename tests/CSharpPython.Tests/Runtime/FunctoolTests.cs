using System;
using static CSharpPython.Tests.Vm.Py;

namespace CSharpPython.Tests.Runtime
{
    public class FunctoolTests
    {
        private static PyFunctool Make(FunctoolMode mode, Value func, Value[] args, string[] kwNames = null, Value[] kwValues = null, long maxSize = -1) =>
            new PyFunctool(mode, func, args, kwNames, kwValues, maxSize);

        private static readonly Value Callable = Value.FromNative(new NativeFunction("f", (c, a) => Value.None));

        [Test]
        public void TheShapeOfEachModeIsChecked()
        {
            var none = Array.Empty<Value>();
            Assert.That(Make(FunctoolMode.Partial, Callable, none).IsConsistent(), Is.True);
            Assert.That(Make(FunctoolMode.Partial, Value.None, none).IsConsistent(), Is.False);
            Assert.That(Make(FunctoolMode.Partial, Callable, none, new[] { "a" }, none).IsConsistent(), Is.False, "names and values differ in number");
            Assert.That(Make(FunctoolMode.Cached, Callable, none).IsConsistent(), Is.True);
            Assert.That(Make(FunctoolMode.Cached, Callable, new[] { Value.FromInt(1) }).IsConsistent(), Is.False);
            Assert.That(Make(FunctoolMode.Cached, Callable, none, maxSize: -5).IsConsistent(), Is.False);
            Assert.That(new PyFunctool(FunctoolMode.Cached, Callable, none) { Hits = -1 }.IsConsistent(), Is.False);
            Assert.That(Make(FunctoolMode.CacheMaker, Value.None, none, maxSize: 3).IsConsistent(), Is.True);
            Assert.That(Make(FunctoolMode.CacheMaker, Value.None, new[] { Value.FromInt(1) }).IsConsistent(), Is.False);
            Assert.That(Make(FunctoolMode.Wraps, Callable, none).IsConsistent(), Is.True);
            Assert.That(Make(FunctoolMode.Wraps, Callable, new[] { Value.FromInt(1) }).IsConsistent(), Is.False);
            Assert.That(Make(FunctoolMode.ItemGetter, Value.None, none).IsConsistent(), Is.False);
            Assert.That(Make(FunctoolMode.ItemGetter, Value.None, new[] { Value.FromInt(1) }).IsConsistent(), Is.True);
            Assert.That(Make(FunctoolMode.AttrGetter, Value.None, new[] { Value.FromInt(1) }).IsConsistent(), Is.False);
            Assert.That(Make(FunctoolMode.AttrGetter, Value.None, new[] { Value.FromString("x") }).IsConsistent(), Is.True);
            Assert.That(Make(FunctoolMode.MethodCaller, Value.None, new[] { Value.FromInt(1) }).IsConsistent(), Is.False);
            Assert.That(Make(FunctoolMode.MethodCaller, Value.None, new[] { Value.FromString("m"), Value.FromInt(1) }).IsConsistent(), Is.True);
            Assert.That(Make((FunctoolMode)99, Value.None, none).IsConsistent(), Is.False);
        }

        [Test]
        public void TheCacheKeepsTheMostRecentlyUsed()
        {
            var cached = Make(FunctoolMode.Cached, Callable, Array.Empty<Value>(), maxSize: 2);
            Value K(int n) => cached.CacheKey(new[] { Value.FromInt(n) }, Array.Empty<string>(), Array.Empty<Value>());
            Assert.That(cached.TryHit(K(1), out _), Is.False);
            cached.Store(K(1), Value.FromInt(10));
            cached.Store(K(2), Value.FromInt(20));
            Assert.That(cached.TryHit(K(1), out Value one), Is.True);
            Assert.That(one.AsInt(), Is.EqualTo(10));
            cached.Store(K(3), Value.FromInt(30)); // 2 was used least recently
            Assert.That(cached.TryHit(K(2), out _), Is.False);
            Assert.That(cached.TryHit(K(1), out _), Is.True);
            Assert.That(cached.Hits, Is.EqualTo(2));
            cached.ClearCache();
            Assert.That(cached.Cache.Count, Is.EqualTo(0));
            Assert.That(cached.Hits + cached.Misses, Is.EqualTo(0));
        }

        [Test]
        public void ANeverCachingWrapperAlwaysMisses()
        {
            var cached = Make(FunctoolMode.Cached, Callable, Array.Empty<Value>(), maxSize: 0);
            cached.Store(Value.FromInt(1), Value.FromInt(1));
            Assert.That(cached.TryHit(Value.FromInt(1), out _), Is.False);
            Assert.That(cached.Cache.Count, Is.EqualTo(0));
            Assert.That(cached.Misses, Is.EqualTo(1));
        }

        [Test]
        public void TypedKeysKeepTypesApart()
        {
            var typed = new PyFunctool(FunctoolMode.Cached, Callable, Array.Empty<Value>(), typed: true);
            var key1 = typed.CacheKey(new[] { Value.FromInt(1), Value.FromFloat(1.0) }, new[] { "k" }, new[] { Value.FromInt(1) });
            var key2 = typed.CacheKey(new[] { Value.FromInt(1), Value.FromFloat(1.0) }, new[] { "k" }, new[] { Value.FromFloat(1.0) });
            Assert.That(Ops.Equal(key1, key2), Is.False);
        }

        [Test]
        public void WrongCallsAreTypeErrors()
        {
            Assert.That(Fails("import operator\noperator.itemgetter(1)(1, 2)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("import operator\noperator.itemgetter(1)(x=1)").Problem, Does.Contain("no keyword"));
            Assert.That(Fails("import functools\nfunctools.lru_cache(maxsize=1, bad=2)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("import functools\nfunctools.lru_cache(maxsize=1)(5)").Problem, Does.Contain("callable"));
            Assert.That(Fails("import functools\nfunctools.cache(5)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("import functools\nfunctools.lru_cache(maxsize=2)(len)()").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("import functools, operator\nfunctools.wraps(len)(1, 2)").TypeName, Is.EqualTo("TypeError"));
            Assert.That(Fails("import operator\noperator.methodcaller('x', k=1)").TypeName, Is.EqualTo("TypeError"));
        }

        [Test]
        public void ReprsAndKeywordForms()
        {
            Assert.That(Out("import functools\ndef f(a, b=0): return a + b\np = functools.partial(f, 1, b=2)\nprint(repr(p).startswith('functools.partial(<function f'), repr(p)[-9:], p(), p(b=5))"), Is.EqualTo("True , 1, b=2) 3 6"));
            Assert.That(Out("import functools\nc = functools.lru_cache(maxsize=1)(len)\nprint(repr(c).startswith('<functools._lru_cache_wrapper object at 0x'), c.__wrapped__ is len)"), Is.EqualTo("True True"));
            Assert.That(Out("import functools\nprint(functools.lru_cache(maxsize=None, typed=True)(len)('ab'), functools.lru_cache(typed=True)(len)('abc'))"), Is.EqualTo("2 3"));
        }

        [Test]
        public void RecursionThroughACacheIsAsDeepAsOrdinaryRecursion()
        {
            Assert.That(Out("import functools\n@functools.cache\ndef total(n):\n    return 0 if n == 0 else n + total(n - 1)\nprint(total(150))"), Is.EqualTo("11325"));
        }

        [Test]
        public void ACachedCallThatRaisesIsNotRemembered()
        {
            Assert.That(Out("import functools\nn = [0]\n@functools.cache\ndef f(x):\n    n[0] += 1\n    if n[0] < 3:\n        raise ValueError(x)\n    return x\nfor i in range(3):\n    try:\n        print(f(7))\n    except ValueError:\n        print('E')\nprint(f(7), n, f.cache_info())"),
                Is.EqualTo("E\nE\n7\n7 [3] CacheInfo(hits=1, misses=3, maxsize=None, currsize=1)"));
        }
    }
}
