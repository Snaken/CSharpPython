import random

def t(f):
    try:
        r = f()
        print(type(r).__name__, r)
    except Exception as e:
        print(type(e).__name__)

random.seed(2024)
print([random.choice([1, 2, 3, 4, 5]) for i in range(10)])
print([random.choice('abcdef') for i in range(10)])
print([random.choice((10, 20, 30)) for i in range(6)])
print([random.choice(range(100, 200)) for i in range(6)])
print(random.choice([42]), random.choice('x'))

items = list(range(10))
random.shuffle(items)
print(items)
random.shuffle(items)
print(items)
words = ['a', 'b', 'c', 'd', 'e', 'f', 'g']
random.shuffle(words)
print(words)
one = [1]
random.shuffle(one)
none = []
random.shuffle(none)
print(one, none)
big = list(range(100))
random.shuffle(big)
print(big[:10], sorted(big) == list(range(100)))
t(lambda: random.shuffle((1,)))
t(lambda: random.shuffle(()))
t(lambda: random.shuffle((1, 2)))
t(lambda: random.shuffle('ab'))
t(lambda: random.shuffle(5))
t(lambda: random.choice([]))
t(lambda: random.choice(''))
t(lambda: random.choice(()))
t(lambda: random.choice(range(0)))
t(lambda: random.choice(5))
t(lambda: random.choice())
t(lambda: random.choice({1, 2}))

class Seq:
    def __len__(self):
        return 3
    def __getitem__(self, i):
        return i * 10

random.seed(1)
print(random.choice(Seq()), random.choice(Seq()))
