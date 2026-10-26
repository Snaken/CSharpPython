import random

def t(f):
    try:
        r = f()
        print(type(r).__name__, r)
    except Exception as e:
        print(type(e).__name__)

random.seed(1234)
print([random.randrange(10) for i in range(12)])
print([random.randrange(5, 15) for i in range(12)])
print([random.randrange(0, 100, 7) for i in range(12)])
print([random.randrange(10, 0, -3) for i in range(12)])
print([random.randrange(-5, 5) for i in range(12)])
print([random.randint(1, 6) for i in range(20)])
print([random.randint(-3, -3) for i in range(3)])
print(random.randrange(2**70), random.randrange(2**70, 2**71), random.randint(0, 2**100))
print(random.randrange(1), random.randrange(3, 4), random.randrange(10, 11, 5))
print(random.randrange(start=3, stop=9), random.randint(a=1, b=1))

t(lambda: random.randrange(0))
t(lambda: random.randrange(-3))
t(lambda: random.randrange(5, 5))
t(lambda: random.randrange(5, 1))
t(lambda: random.randrange(1, 5, 0))
t(lambda: random.randrange(5, 1, 2))
t(lambda: random.randrange(1, 5, -1))
t(lambda: random.randrange(1.5))
t(lambda: random.randrange(1, 5.0))
t(lambda: random.randrange('a'))
t(lambda: random.randrange(5, None, 2))
t(lambda: random.randrange(5, None, 1))
t(lambda: random.randrange())
t(lambda: random.randint(5, 1))
t(lambda: random.randint(1))
t(lambda: random.randint(1.0, 2))
t(lambda: random.randint(True, True))

class Index:
    def __index__(self):
        return 4

random.seed(9)
print(random.randrange(Index()), random.randint(Index(), Index()))
