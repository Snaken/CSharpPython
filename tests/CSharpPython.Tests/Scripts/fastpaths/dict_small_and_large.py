# A dictionary with a few string keys is searched in place; these cross every boundary of that.
d = {}
for i in range(20):
    d['k%d' % i] = i
    if i in (0, 3, 7, 8, 9, 15):
        print(len(d), list(d)[-1], d['k%d' % i])
for i in range(0, 20, 3):
    del d['k%d' % i]
print(len(d), list(d))
d['k0'] = 'again'
d['k3'] = 'back'
print(list(d.items())[-3:], 'k0' in d, 'k1' in d, 'zz' in d)

small = {'a': 1, 'b': 2}
small[1] = 'int key'
small[1.0] = 'float key replaces it'
small[True] = 'so does this'
small[None] = 'none'
small[('t', 1)] = 'tuple'
print(small, small[1], len(small))
print('a' in small, 1 in small, 1.0 in small, (('t', 1)) in small, None in small, 'b' in small)

for key in ([], {}, set(), [1, 2]):
    for target in ({}, {'a': 1}, dict.fromkeys('abcdefghij')):
        try:
            target[key]
        except TypeError:
            print('TypeError')
        try:
            key in target
        except TypeError as e:
            print('TypeError in')
        try:
            target[key] = 1
        except TypeError:
            print('TypeError set')

order = {}
for name in ['z', 'y', 'x']:
    order[name] = 0
del order['y']
order['y'] = 1
order['w'] = 2
print(list(order), list(order.values()))
order.update({'z': 9, 'v': 8})
print(order, order.pop('z'), order.popitem(), order)

copy = dict(d)
copy['extra'] = 1
print(len(copy), len(d), copy == d, {'a': 1, 'b': 2} == {'b': 2, 'a': 1})

counts = {}
for word in 'one two three two three three'.split():
    counts[word] = counts.get(word, 0) + 1
print(counts, sorted(counts, key=counts.get))
for k in list(counts):
    del counts[k]
print(counts, len(counts), bool(counts))
counts['again'] = 1
print(counts)

try:
    for k in d:
        d['new%s' % k] = 1
except RuntimeError as e:
    print('RuntimeError')
try:
    for k in small:
        del small[k]
except RuntimeError:
    print('RuntimeError')

class Point:
    def __init__(self):
        for i in range(12):
            setattr(self, 'f%d' % i, i)


p = Point()
print(len(vars(p)), list(vars(p))[:3], list(vars(p))[-2:], p.f11, hasattr(p, 'f12'))
del p.f5
p.f5 = 'last'
print(list(vars(p))[-2:], p.f5)


class Tiny:
    def __init__(self):
        self.a = 1
        self.b = 2

    def drop(self):
        del self.a
        self.a = 3
        self.c = 4


t = Tiny()
t.drop()
print(vars(t), t.a, t.b, t.c)
try:
    t.zzz
except AttributeError:
    print('AttributeError')
