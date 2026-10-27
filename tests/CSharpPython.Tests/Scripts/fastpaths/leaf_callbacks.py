# Small functions called by built-ins such as sorted, min and map run without a frame; anything unusual still works.
words = ['pear', 'fig', 'banana', 'kiwi']
print(sorted(words, key=lambda w: len(w)), min(words, key=lambda w: -len(w)), max(words, key=lambda w: w[0]))
print(list(map(lambda x: x * 2, range(5))), list(filter(lambda x: x % 2, range(8))), list(map(lambda a, b: a - b, [5, 6], [1, 2])))
print(sorted([3, 1, 2], key=lambda v: -v), sorted([1.5, 0.5, 1.0], key=lambda v: v * -1), sorted(range(5), key=lambda v: v % 3))
print(sorted([2 ** 62, 1, 2 ** 63], key=lambda v: v * 4), sorted([5, 3], key=lambda v: v - 2 ** 63))
print(min([4, 2, 9], key=lambda v: v / 2), min([4, 2, 9], key=lambda v: v // 2), max([4, 2, 9], key=lambda v: v % 5))
print(sum(map(lambda v: v * 0.5, range(10))), list(map(lambda v: v / 3, range(4))))

offset = 10
def make(step):
    return lambda v: v + step + offset

print(list(map(make(1), range(3))), sorted(range(4), key=make(-1)))

class Item:
    def __init__(self, n, w):
        self.n = n
        self.w = w

items = [Item('a', 3), Item('b', 1), Item('c', 2)]
print([i.n for i in sorted(items, key=lambda i: i.w)], max(items, key=lambda i: i.w).n)
print(sorted([(2, 'b'), (1, 'z'), (1, 'a')], key=lambda t: t[0]), sorted(['b', 'a'], key=lambda s: s == 'b'))
print(sorted([[3, 1], [1, 2]], key=lambda l: l[-1]), sorted(['ab', 'c'], key=lambda s: s[0]))
table = {1: 'one', 2: 'two'}
print(list(map(lambda k: table[k], [2, 1])), list(filter(lambda k: k in table, [1, 5])))
print(list(map(lambda v: 'big' if v > 2 else 'small', range(5))), list(map(lambda v: v > 1 and v < 4, range(5))))
print(list(map(lambda v: v or 'none', [0, 1, '', 'x', None])), list(map(lambda v: not v, [0, 1, '', 'x', None, 2.5])))

# Errors still carry the usual message, and a function that is not plain runs the ordinary way.
for f, data in [(lambda v: 1 / v, [1, 0]), (lambda v: v[5], [[1]]), (lambda v: v + 'x', [1]), (lambda v: missing, [1]),
                (lambda v: v.nope, [Item(1, 2)]), (lambda v: table[v], [3]), (lambda v: -v, ['a']), (lambda v: v % 0, [3])]:
    try:
        list(map(f, data))
    except Exception as e:
        print(type(e).__name__)

print(sorted(['b', 'a', 'c'], key=lambda s: s), sorted([3, 1, 2], key=abs), sorted([1, 2, 3], key=lambda v: (v % 2, v)))
print(sorted([1, 2, 3], key=lambda v: str(v)), list(map(lambda v: [v], range(3))), list(map(lambda v: len('a' * v), range(3))))

def shout(s):
    return s.upper()

print(list(map(shout, ['a', 'b'])), list(map(lambda s: s.upper(), ['a'])))
print(sorted([True, False, True], key=lambda b: b), list(map(lambda b: b + 1, [True, False])))
print(list(map(lambda v: -v, [0.0, -0.0, 5, -5])), list(map(lambda x, y: x * y, [2 ** 40], [2 ** 40])))
print(list(map(lambda v: v == 2.0, [2, 2.0, 'a'])), list(map(lambda v: v == 'a', ['a', 'b', 1])), list(map(lambda v: 3 < v, [2, 4.5])))
print(list(map(lambda v: (v / 3) * 3, range(4))), list(map(lambda v: 2 ** 60 * v / 7, [1])))

import math
scores = {'a': 3, 'b': 1}
print(list(map(lambda v: round(v * math.pi, 3), [1, 2])), list(map(lambda k: scores[k], ['b', 'a'])), list(map(lambda v: v[-1], [(1, 2), [3, 4]])))
print(list(map(lambda v: v[0], ['ab', 'cd'])), list(map(lambda v: v[2], [(1, 2)] * 0)), list(map(lambda k: scores[k] * 2, 'ab')))
print(list(map(lambda v: v // 3, [7, -7, 0])), list(map(lambda v: v % 3, [7, -7, 0])), list(map(lambda v: v // -3, [7])), list(map(lambda v: v % -3, [7])))
print(list(map(lambda v: v * 3, [2 ** 31, 2 ** 31 - 1, -2 ** 31])), list(map(lambda v: v + v, [2 ** 62, 2 ** 63 - 1])), list(map(lambda v: v - 1, [-2 ** 63])))
print(list(map(lambda v: v / 2, [2 ** 60 + 1, 2 ** 53, 7])), list(map(lambda v: v < 2.5, [2, 3, 2 ** 60])), list(map(lambda v: v != 3, [3, 3.0, 4])))
print(list(map(lambda v: v * 1.5 - 0.5 + 2, [1, 2.5])), list(map(lambda v: -v if v else v, [0, 3, 2.5])), list(map(lambda v: v and 1, [0, 5, '', 'x'])))
print(list(map(lambda v: 1 / v, [2, 4.0])), list(map(lambda v: v == v, [float('nan'), 1])), list(map(lambda v: v < v, ['a'])))
print(list(map(lambda v, k=3: v * k, [1, 2])), list(map(lambda v, k=3: v * k, [1, 2], [5, 6])), sorted([2, 1], key=lambda v=0: -v))

# A key looked up in a dictionary can run the script's own __eq__ in the middle of a leaf function.
class K:
    def __init__(self, n): self.n = n
    def __hash__(self): return self.n
    def __eq__(self, o): return isinstance(o, K) and self.n == o.n

d = {1: 20, K(1): 10}
print(list(map(lambda k: d[k] + k, [1])), list(map(lambda k: d[k], [K(1)])))
print(list(map(lambda v: v * math.pi > 3, [1, 0])), list(map(lambda v: not v, [[], [1]])), end=' ')
try:
    list(map(lambda v: v / 0.0, [1.5]))
except ZeroDivisionError:
    print('ZeroDivisionError')
e = {K(2): 10, 2: 20, K(3): 30}
print(list(map(lambda k: e[k] + k, [2, 2])), list(map(lambda k: e[k], [K(2), 2])), list(map(lambda k: k in e, [2])))
