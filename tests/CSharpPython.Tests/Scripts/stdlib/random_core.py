import random

# The same seed gives the same numbers as CPython, for small, negative and very large seeds.
for seed in (0, 1, 42, -5, 2**32, 2**64 + 7, 12345678901234567890123, True):
    random.seed(seed)
    print(seed, random.random(), random.getrandbits(5), random.getrandbits(32), random.getrandbits(33), random.getrandbits(100), random.getrandbits(0))

random.seed(7)
first = [random.random() for i in range(5)]
random.seed(7)
second = [random.random() for i in range(5)]
print(first == second, first)

random.seed(8)
print(first == [random.random() for i in range(5)])

random.seed(a=3)
print(random.random())
random.seed(None)
print(0.0 <= random.random() < 1.0)

def t(f):
    try:
        r = f()
        print(type(r).__name__, r)
    except Exception as e:
        print(type(e).__name__)

t(lambda: random.getrandbits(-1))
t(lambda: random.getrandbits(1.5))
t(lambda: random.getrandbits())
t(lambda: random.random(1))
t(lambda: random.seed(1, 2))
t(lambda: random.seed(b=3))
t(lambda: random.seed(a=1, **{'a': 2}))
