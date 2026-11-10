import math
import random
xs = [3, 1, 2]
d = {"a": 1}
s = "hello"
print(len(xs), abs(-3), min(2, 5), max(xs), sum(xs), isinstance(xs, list), str(12), int("7"))
print(xs.count(1), xs.index(2), d.get("a"), d.get("z", 9), s.upper(), s.replace("l", "L"), s.split("e"))
print(math.sqrt(16), math.floor(2.5), math.pow(2, 3), round(2.567, 1))
f = len
g = xs.append
g(4)
print(f(xs), xs)
ms = math.sqrt
print(ms(25))
# keyword arguments take the general path
print(sorted(xs, reverse=True), sorted(xs, key=lambda v: -v), max(xs, key=lambda v: -v), int("ff", base=16))
# wrong argument counts raise TypeError, through every kind of native
tests = [
    lambda: len(), lambda: len(1, 2), lambda: abs(), lambda: xs.append(), lambda: xs.append(1, 2),
    lambda: xs.count(), lambda: d.get(), lambda: d.get(1, 2, 3), lambda: s.upper(1), lambda: math.sqrt(),
    lambda: math.sqrt(1, 2), lambda: isinstance(1), lambda: len(xs, key=1), lambda: random.random(1),
]
for t in tests:
    try:
        t()
    except TypeError:
        print("TypeError")
# errors raised by the native itself
for t in (lambda: len(5), lambda: math.sqrt(-1), lambda: int("x"), lambda: xs.index(99), lambda: abs("a")):
    try:
        t()
    except Exception as e:
        print(type(e).__name__)
# generators stepped with next()/send() are not plain native calls
def gen():
    got = yield 1
    yield got
it = gen()
print(next(it), it.send(7), next(iter([5])))
# natives called with a shadowed builtin name
def len(v):
    return "mine"
print(len(xs))
# a native passed to another native and called back
print(list(map(abs, [-1, -2])), sorted(["b", "a"], key=str.upper), list(filter(None, [0, 1, 2])))
