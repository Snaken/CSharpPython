# dict methods

d = {'a': 1, 'b': 2}
print(d.get('a'), d.get('z'), d.get('z', 0))

d = {'a': 1, 'b': 2}
print(list(d.keys()), list(d.values()), list(d.items()))

d = {'a': 1, 'b': 2}
print(d.pop('a'), d)

d = {'a': 1}
print(d.pop('z', 'dflt'), d)

d = {}
print(d.setdefault('a', []), d.setdefault('a', 5), d)

d = {}
d.setdefault('a')
print(d)

d = {'a': 1}
d.update({'b': 2, 'a': 3})
print(d)

d = {'a': 1}
d.update([('c', 4)])
print(d)

d = {'a': 1}
d.update(b=5)
print(d)

d = {'a': 1}
d.clear()
print(d, len(d))

d = {'a': [1]}
e = d.copy()
e['b'] = 2
e['a'].append(2)
print(d, e)

d = {1: 'a', 2: 'b', 3: 'c'}
for k in d:
    print(k, d[k])

d = {'a': 1, 'b': 2}
for k, v in d.items():
    print(k, v)

d = {'x': 1}
d['y'] = 2
d['x'] = 3
print(d)

d = {}
for i in range(5):
    d[i % 2] = d.get(i % 2, 0) + i
print(d)

d = {'a': 1, 'b': 2, 'c': 3}
d.pop('b')
d['d'] = 4
d['b'] = 5
print(d)

d = {}
for i in range(100):
    d[i] = i
for i in range(0, 100, 2):
    d.pop(i)
print(len(d), list(d)[:3], sum(d.values()))

d = {'a': 1}
print('a' in d, 'b' in d, 'a' not in d, len(d))

d = {1: 'a'}
d[1.0] = 'b'
d[True] = 'c'
print(d)

print(1)

d = {'a': 1, 'b': 2}
print(sorted(d.items()), sorted(d), max(d), len(d.values()))

d = {(1, 2): 'p', 'k': (3, 4)}
print(d[(1, 2)], d['k'][1])

t = (1, 2, 2, 3)
print(t.index(2), t.count(2), t.count(9), t.index(3))

d = {}
d[(1, 2)] = 1
d[(1, 2)] += 1
print(d)

d = dict(a=1)
d['b'] = d.get('a') + 1
print(d)

x = {'a': 1}
y = dict(x)
y['a'] = 2
print(x, y)

d = {'a': 1}
d.pop('a')
print(d, bool(d))
