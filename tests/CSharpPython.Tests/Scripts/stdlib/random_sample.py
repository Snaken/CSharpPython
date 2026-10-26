import random

def t(f):
    try:
        r = f()
        print(type(r).__name__, r)
    except Exception as e:
        print(type(e).__name__)

random.seed(55)
# A small population is copied into a pool; a large one with few picks tracks the picks in a set.
print(random.sample(range(10), 3))
print(random.sample(range(10), 10))
print(random.sample(list('abcdefghij'), 4))
print(random.sample((1, 2, 3, 4, 5, 6, 7, 8), 8))
print(random.sample('hello world', 5))
print(random.sample(range(100), 6))
print(random.sample(range(1000), 20))
print(random.sample(range(10**9), 5))
print(random.sample(range(10, 1000, 7), 10))
print(random.sample(range(100, 0, -1), 5))
print(random.sample([], 0), random.sample([1], 1), random.sample(range(5), 0))
print(random.sample(range(300), 100)[:10])
print(len(set(random.sample(range(500), 300))))
print(random.sample(population=[1, 2, 3, 4, 5, 6, 7], k=3))
print(random.sample([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12], k=6))

t(lambda: random.sample(range(5), 6))
t(lambda: random.sample(range(5), -1))
t(lambda: random.sample([1, 2, 3], 2.0))
t(lambda: random.sample({1, 2, 3}, 2))
t(lambda: random.sample({'a': 1}, 1))
t(lambda: random.sample(5, 1))
t(lambda: random.sample([1, 2, 3]))
t(lambda: random.sample([1, 2, 3], 1, 2))
t(lambda: random.sample([1, 2, 3], k=1, bogus=1))
