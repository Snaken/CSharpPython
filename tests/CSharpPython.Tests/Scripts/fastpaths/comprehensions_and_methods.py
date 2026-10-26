# Comprehensions add to their result directly; a method call site remembers what it found.
print([x * x for x in range(6)], [x for x in range(10) if x % 3 == 0], [(a, b) for a in range(3) for b in range(a)])
print({x % 3 for x in range(10)}, {k: v for k, v in zip('abc', range(3))}, [[y for y in range(x)] for x in range(4)])
print([x for x in 'hello' if x != 'l'], [len(w) for w in 'a bb ccc'.split()], sum(x for x in range(5)))
print({(1, 2), (1, 2)}, {x for x in [1, 1.0, True]}, [x for x in []], {x for x in []})
try:
    {x for x in [[1], [2]]}
except TypeError:
    print('TypeError')
try:
    [y for y in 5]
except TypeError:
    print('TypeError')

squares = [n * n for n in range(4)]
squares2 = [n for n in squares]
squares2.append(99)
print(squares, squares2)
text = 'x'
print([text * n for n in range(3)], [text for text in 'ab'], text)

from collections import deque, Counter, defaultdict
for thing, wanted in [([1, 2, 2, 3], 2), ('abba', 'b'), (deque([1, 2, 2]), 2), ((1, 2, 2), 2)]:
    for round in range(2):
        print(type(thing).__name__, thing.count(wanted), thing.index(wanted))
for thing in [[1, 2], deque([1, 2]), {'a': 1}, Counter('ab'), defaultdict(int, {'a': 1}), {1, 2}, frozenset({1})]:
    for round in range(2):
        print(type(thing).__name__, type(thing.copy()).__name__)
for thing in [{'a': 1}, Counter('aab'), defaultdict(list, {'a': 1}), {'a': 1}]:
    print(thing.get('a'), thing.get('zz'), thing.get('zz', 5), thing.setdefault('q', 7), type(thing).__name__)
for thing in [[3, 1, 2], deque([3, 1, 2]), [3, 1, 2]]:
    thing.append(0)
    print(thing.pop(), thing.index(1), thing.count(3), len(thing))
for s in ['a,b', 'c,d,e', ' f ']:
    print(s.split(','), s.strip(), s.upper(), s.replace(',', ';'), s.startswith('a'))
for thing in [{1, 2}, frozenset({1, 2}), {3}]:
    try:
        thing.add(9)
        print(sorted(thing))
    except AttributeError:
        print('AttributeError')
    print(thing.copy() == thing, type(thing).__name__)

class Box:
    def append(self, x):
        return ('box', x)

    def get(self, k):
        return 'box get'


for thing in [[], Box(), [], Box(), {}, Box()]:
    print(thing.append(1) if hasattr(thing, 'append') else thing.get('a'))
for i in range(3):
    item = [] if i % 2 == 0 else Box()
    print(item.append(i))
