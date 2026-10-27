import functools
import operator
from functools import partial, lru_cache, cache, wraps, reduce

def power(base, exponent=2, *, scale=1):
    return base ** exponent * scale

square = partial(power, exponent=2)
cube = partial(power, exponent=3)
print(square(5), cube(2), partial(power, 3)(), partial(power, 2, 5)(), partial(power, scale=10)(3), cube(2, scale=2), square(base=4), cube(3, exponent=1))
print(list(map(partial(power, exponent=3), range(4))), sorted([3, 1, 2], key=partial(operator.mul, -1)), reduce(partial(operator.add), [1, 2, 3]))
nested = partial(partial(power, 2), 3)
print(nested(), partial(max, 10)(3, 20), partial(int, '101')(2), partial(print, 'a', end='|')('b'))
print(square.func is power, square.args, square.keywords, partial(power, 1, 2, scale=3).args, partial(power, 1, 2, scale=3).keywords, callable(square))
print(type(square).__name__, partial(str.join, ',')(['a', 'b']))
adders = [partial(operator.add, n) for n in range(3)]
print([f(10) for f in adders])

class Counter:
    def __init__(self):
        self.n = 0
    def bump(self, by, extra=0):
        self.n += by + extra
        return self.n

c = Counter()
bump2 = partial(c.bump, 2)
print(bump2(), bump2(extra=10), c.n)

for bad in (lambda: partial(), lambda: partial(5), lambda: partial(power)(), lambda: partial(power, 1, 2, 3)()):
    try:
        bad()
    except TypeError:
        print('TypeError')

@lru_cache(maxsize=None)
def fib(n):
    return n if n < 2 else fib(n - 1) + fib(n - 2)

print(fib(80), fib.cache_info())
fib.cache_clear()
print(fib.cache_info(), fib(10), fib.cache_info())

calls = []

@lru_cache
def slow(a, b=1):
    calls.append((a, b))
    return a * b

print(slow(2), slow(2), slow(2, 1), slow(2, b=1), slow(3), slow(3, 2), calls, slow.cache_info())

@lru_cache(maxsize=2)
def small(x):
    calls.append(x)
    return x * x

del calls[:]
print(small(1), small(2), small(1), small(3), small(2), small(1), calls, small.cache_info())

@lru_cache()
def typed_default(x):
    return type(x).__name__

@lru_cache(maxsize=4, typed=True)
def typed(x):
    return type(x).__name__

print(typed_default(1), typed_default(1.0), typed_default.cache_info(), typed(1), typed(1.0), typed(True), typed.cache_info())

@cache
def square_cached(x):
    calls.append(('sq', x))
    return x * x

del calls[:]
print(square_cached(4), square_cached(4), calls, square_cached.cache_info().hits, square_cached.cache_info().maxsize)

@lru_cache(maxsize=0)
def never(x):
    return x

print(never(1), never(1), never.cache_info())
print(lru_cache(maxsize=1)(len)('abc'), cache(len)((1, 2)))

@lru_cache(None)
def bad_key(x):
    return x

for arg in ([1], {'a': 1}, {1}):
    try:
        bad_key(arg)
    except TypeError:
        print('unhashable')
print(bad_key((1, 2)), bad_key('s'), bad_key(None), bad_key.cache_info().currsize)

@lru_cache(maxsize=None)
def fails(x):
    if x < 0:
        raise ValueError('neg')
    return x

try:
    fails(-1)
except ValueError:
    print('ValueError')
print(fails(1), fails.cache_info())

class Grid:
    def __init__(self, n):
        self.n = n
    @lru_cache(maxsize=None)
    def area(self, k):
        return self.n * k

g = Grid(3)
print(g.area(2), g.area(2), Grid.area.cache_info().hits, callable(fib), fib.__wrapped__(5), type(fib).__name__)

def logged(f):
    @wraps(f)
    def wrapper(*args, **kwargs):
        calls.append(f.__name__)
        return f(*args, **kwargs)
    return wrapper

@logged
def hello(name, punct='!'):
    return 'hello ' + name + punct

del calls[:]
print(hello('bob'), hello('al', punct='?'), calls)

try:
    lru_cache(maxsize='x')
except TypeError:
    print('TypeError')
try:
    fib.nope
except AttributeError:
    print('AttributeError')

people = [('bob', 25, {'city': 'ny'}), ('al', 30, {'city': 'la'}), ('cy', 20, {'city': 'sf'})]
print(sorted(people, key=operator.itemgetter(1)), sorted(people, key=operator.itemgetter(0, 1))[0], list(map(operator.itemgetter(2), people))[0])
print(operator.itemgetter(1)('abc'), operator.itemgetter('k')({'k': 5}), operator.itemgetter(0, 2)([7, 8, 9]), operator.itemgetter(-1)((1, 2)))

class P:
    def __init__(self, x, y):
        self.x, self.y = x, y
        self.pos = self
    def total(self, extra=0):
        return self.x + self.y + extra

pts = [P(3, 1), P(1, 2), P(2, 9)]
print([p.x for p in sorted(pts, key=operator.attrgetter('x'))], operator.attrgetter('x', 'y')(pts[0]), operator.attrgetter('pos.x')(pts[1]), max(pts, key=operator.attrgetter('y')).y)
print(operator.methodcaller('total')(pts[0]), operator.methodcaller('total', 10)(pts[1]), operator.methodcaller('upper')('ab'), list(map(operator.methodcaller('count', 'a'), ['aa', 'ba'])))
for bad in (lambda: operator.itemgetter(), lambda: operator.itemgetter(1)(), lambda: operator.itemgetter(5)([1]), lambda: operator.attrgetter('nope')(P(1, 2)), lambda: operator.attrgetter(5),
            lambda: operator.methodcaller(), lambda: operator.methodcaller(5), lambda: operator.itemgetter(1)(1, 2)):
    try:
        bad()
    except (TypeError, IndexError, AttributeError) as e:
        print(type(e).__name__)
print(type(operator.itemgetter(1)).__name__, type(operator.attrgetter('x')).__name__, type(operator.methodcaller('x')).__name__)
