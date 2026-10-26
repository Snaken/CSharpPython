import random

def t(f):
    try:
        r = f()
        print(type(r).__name__, r)
    except Exception as e:
        print(type(e).__name__)

random.seed(31337)
print(random.choices([1, 2, 3, 4, 5]))
print(random.choices([1, 2, 3, 4, 5], k=10))
print(random.choices('abc', k=8))
print(random.choices(range(100), k=5))
print(random.choices(['red', 'green', 'blue'], weights=[10, 1, 1], k=12))
print(random.choices(['red', 'green', 'blue'], [5, 0, 1], k=8))
print(random.choices(['a', 'b', 'c'], cum_weights=[1, 3, 6], k=10))
print(random.choices(['a', 'b'], weights=[0.5, 1.5], k=6))
print(random.choices(['x'], weights=[3], k=3))
print(random.choices([1, 2, 3], k=0))
print(random.choices([1, 2, 3], weights=[1, 1, 1], k=5))
print(random.choices([1, 2, 3], weights=(2, 2, 2), k=5))

t(lambda: random.choices([]))
t(lambda: random.choices([], k=0))
t(lambda: random.choices([1, 2], weights=[1]))
t(lambda: random.choices([1, 2], weights=[0, 0]))
t(lambda: random.choices([1, 2], weights=[-1, -1]))
t(lambda: random.choices([1, 2], weights=[1, 1], cum_weights=[1, 2]))
t(lambda: random.choices([1, 2], cum_weights=[1]))
t(lambda: random.choices([1, 2], weights=[float('inf'), 1]))
t(lambda: random.choices([1, 2], weights=['a', 'b']))
t(lambda: random.choices([1, 2], 3))
t(lambda: random.choices([1, 2], k=1.5))
t(lambda: random.choices())
t(lambda: random.choices(5))
