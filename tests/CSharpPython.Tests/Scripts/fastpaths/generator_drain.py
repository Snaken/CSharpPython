# any, all, sum, list, tuple, sorted and set take the values of a generator without leaving the interpreter between them.
# The generator must still run exactly as far as the consumer asks, and errors must reach the right side.

def counting(n, log):
    for i in range(n):
        log.append('make %d' % i)
        yield i

# any and all stop at the first answer, and the generator stays suspended where it stopped.
log = []
g = counting(10, log)
print(any(x == 3 for x in g), log)
print(next(g), list(g))
log = []
g = counting(10, log)
print(all(x < 4 for x in g), log)
print(next(g), len(list(g)))
log = []
print(any(counting(3, log)), all(counting(3, log)), log)
print(any(x for x in []), all(x for x in []))
g = (x for x in range(5))
print(any(x == 2 for x in g), list(g), any(g), all(g))

# sum
print(sum(x for x in range(1000)), sum((x for x in range(10)), 100), sum(x for x in []), sum((x for x in []), 5))
print(sum(x * 0.5 for x in range(100)), sum((0.1 for _ in range(10)), 0.0), sum((0.1 for _ in range(10))))
print(sum((0.1 for _ in range(10)), 1), sum((x for x in [0.5, 2, 1, 4]), 0.25))
print(sum(x for x in [True, True, False, True]), sum((x for x in [True, 0.5]), 0))
print(sum(2 ** 62 for _ in range(4)), sum(-(2 ** 62) for _ in range(5)), sum(2 ** 70 for _ in range(3)))
print(sum((x for x in [0.5, 2 ** 70]), 0.0), sum((x for x in [1e308, 1e308, -1e308]), 0.0))
print(sum(float('inf') for _ in range(2)), sum(x for x in [float('inf'), float('-inf')]) != 0.0)

class Vec:
    def __init__(self, x):
        self.x = x
    def __add__(self, other):
        return Vec(self.x + (other.x if isinstance(other, Vec) else other))
    def __radd__(self, other):
        return Vec(self.x + other)

print(sum((Vec(i) for i in range(5)), Vec(100)).x, sum(Vec(i) for i in range(5)).x)

class Flag:
    def __init__(self, v):
        self.v = v
    def __bool__(self):
        return self.v

print(any(Flag(i == 3) for i in range(6)), all(Flag(i < 3) for i in range(6)))

# list, tuple, sorted, set
print(list(x * x for x in range(6)), tuple(x for x in 'abc'), sorted((x % 4 for x in range(10)), reverse=True))
print(sorted(set(x % 3 for x in range(10))), len(list(x for x in range(5000))))
print(','.join(str(x) for x in range(5)), max(x for x in [3, 9, 2]), min((x for x in [3, 9, 2]), key=lambda v: -v))

# an error raised by the generator body reaches the caller, and the generator is finished
def failing():
    yield 1
    yield 2
    raise ValueError('boom')

g = failing()
try:
    list(g)
except ValueError as e:
    print('ValueError', e)
print(list(g))
g = failing()
try:
    print(any(x == 99 for x in g))
except ValueError as e:
    print('ValueError', e)
g = failing()
try:
    sum(g)
except ValueError as e:
    print('ValueError', e)
try:
    print(sum(1 // (x - 2) for x in range(5)))
except ZeroDivisionError as e:
    print('ZeroDivisionError', e)

# an error raised by the consumer is not seen by the generator, which stays suspended after its last value
def guarded():
    for v in [1, 2, 'a', 4, 5]:
        try:
            yield v
        except TypeError:
            print('generator saw TypeError')

g = guarded()
try:
    sum(g)
except TypeError as e:
    print('TypeError from sum')
print(list(g))

# a generator that handles its own errors
def tolerant(n):
    for i in range(n):
        try:
            if i % 3 == 0:
                raise KeyError(i)
            yield i
        except KeyError:
            yield -1

print(list(tolerant(7)), sum(tolerant(7)), any(x < 0 for x in tolerant(7)), all(x < 0 for x in tolerant(7)))

# a generator that finishes with return, and one that uses yield from
def returns():
    yield 1
    return 99

print(list(returns()), sum(returns()))

def inner(n):
    for i in range(n):
        yield i
    return 'done'

def outer():
    r = yield from inner(3)
    yield r
    yield from inner(2)

print(list(outer()), any(x == 'done' for x in outer()), sum(x for x in outer() if x != 'done'))

# values sent to a generator that is drained are None
def echo():
    out = []
    for i in range(4):
        v = yield i
        out.append(v)
    yield out

print(list(echo()), sum(x for x in echo() if x != [None, None, None, None] and not isinstance(x, list)))

# generators that drain other generators
def rows(n):
    for r in range(n):
        yield (c for c in range(r + 1))

print([sum(row) for row in rows(5)], sum(sum(row) for row in rows(5)), sum(any(x == 2 for x in row) for row in rows(5)))
print(sum(sum(x * y for y in range(3)) for x in range(4)), [list(r) for r in rows(3)])

def chained(n):
    for i in range(n):
        yield sum(j for j in range(i))

print(list(chained(6)), sum(chained(6)), any(x > 5 for x in chained(6)))

# a generator that drains itself finds that it is running
holder = []
def self_drain():
    yield 1
    try:
        any(holder[0])
    except ValueError as e:
        print('ValueError', e)
    yield 2

holder.append(self_drain())
print(list(holder[0]))

# a generator that is closed or exhausted, drained again
g = (x for x in range(3))
print(list(g), list(g), sum(g), any(g), all(g))

# endless generators are fine for any and all
def endless():
    n = 0
    while True:
        yield n
        n += 1

print(any(x > 1000 for x in endless()), all(x < 1000 for x in endless()))

# the closure and the locals of the generator survive many items
def running_total(items):
    total = 0
    for x in items:
        total += x
        yield total

print(sum(running_total(range(100))), list(running_total([1, 2, 3])), max(running_total(range(10))))

# a try block open at the yield stays open between items
def with_try():
    for i in range(4):
        try:
            yield i
            if i == 2:
                raise RuntimeError('two')
        except RuntimeError:
            yield 'caught'

print(list(with_try()), any(x == 'caught' for x in with_try()))
