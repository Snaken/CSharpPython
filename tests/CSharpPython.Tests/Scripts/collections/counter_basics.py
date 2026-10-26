from collections import Counter

def t(f):
    try:
        r = f()
        print(type(r).__name__, r)
    except Exception as e:
        print(type(e).__name__)

c = Counter('mississippi')
print(c)
print(c['s'], c['z'], 'z' in c, len(c), c)
print(c.most_common(), c.most_common(2), c.most_common(0), c.most_common(100), c.most_common(None))
print(sorted(c.elements()), list(Counter(a=2, b=1).elements()))
print(c.total(), Counter().total(), sum(c.values()))
print(Counter(), Counter([]), Counter({'a': 3, 'b': 0, 'c': -1}), Counter(a=1, b=2))
print(Counter(['a', 'b', 'a', 'c', 'a', 'b']), Counter((1, 2, 2, 3, 3, 3)))
print(Counter('abracadabra').most_common(3))
print(Counter({'x': 2}) == Counter(x=2), Counter(a=1) == Counter(a=1, b=0), Counter(a=1) == {'a': 1}, {'a': 1} == Counter(a=1), Counter(a=1) == Counter(a=2))
print(type(c).__name__, type(c) is Counter, type(c) is dict, isinstance(c, Counter), isinstance(c, dict), isinstance({}, Counter), issubclass(Counter, dict), Counter)

# Updating and subtracting
c = Counter('aab')
c.update('abc')
print(c)
c.update({'a': 10, 'z': 1})
print(c)
c.update(a=1, q=2)
print(c)
c.subtract('aaa')
print(c)
c.subtract({'b': 5})
print(c, c['b'])
c.update()
c.update(None)
print(len(c))
c['new'] += 1
c['a'] -= 100
print(c['new'], c['a'], c)
del c['missing']
del c['new']
print('new' in c)

# Arithmetic keeps only positive counts.
a = Counter(a=3, b=1)
b = Counter(a=1, b=2, c=3)
print(a + b, a - b, a | b, a & b)
print(a + Counter(), Counter() - a, a | Counter(), a & Counter())
a2 = Counter(a=3, b=1)
a2 += b
print(a2)
a2 -= Counter(c=10, a=1)
print(a2)
a2 |= Counter(z=4)
print(a2)
a2 &= Counter(z=2, a=100)
print(a2)
print(type(a + b).__name__, type(a - b).__name__)

# Copies and conversions
d = a.copy()
d['a'] = 0
print(type(d).__name__, a, d)
print(dict(a), type(dict(a)).__name__, list(a), list(a.items()), sorted(a.values()))
print(Counter(a), Counter(a) is a, bool(Counter()), bool(a))
print(Counter(word.lower() for word in 'The cat and the hat and THE bat'.split()))

# Ties keep their order of insertion
t_ = Counter()
for w in ['x', 'y', 'z', 'y', 'x', 'w']:
    t_[w] += 1
print(t_.most_common(), t_)
print(Counter({'a': 1, 'b': 1, 'c': 1}).most_common(2))

t(lambda: Counter(1))
t(lambda: Counter(1, 2))
t(lambda: Counter.fromkeys('abc'))
t(lambda: Counter('ab').update(1))
t(lambda: Counter('ab').most_common('x'))
t(lambda: Counter(a=1) + {'a': 1})
t(lambda: Counter([[1]]))
t(lambda: hash(Counter()))
t(lambda: Counter(a='x') + Counter(a='y'))
