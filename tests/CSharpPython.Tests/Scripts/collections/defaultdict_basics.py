from collections import defaultdict

def t(f):
    try:
        r = f()
        print(type(r).__name__, r)
    except Exception as e:
        print(type(e).__name__)

groups = defaultdict(list)
for word in ['apple', 'avocado', 'banana', 'blueberry', 'cherry']:
    groups[word[0]].append(word)
print(groups)
print(dict(groups))
print(len(groups), list(groups), list(groups.items()))

counts = defaultdict(int)
for ch in 'mississippi':
    counts[ch] += 1
print(counts)
print(counts['z'], counts)
print(counts.get('q'), 'q' in counts, counts.get('q', 5), counts.setdefault('q', 9), counts.pop('q'), 'q' in counts)

nested = defaultdict(lambda: defaultdict(int))
nested['a']['x'] += 2
nested['a']['y'] += 1
nested['b']['x'] += 5
print({k: dict(v) for k, v in nested.items()})

sets = defaultdict(set)
sets['k'].add(1)
sets['k'].add(1)
sets['j'].add(2)
print(sets)
print(defaultdict(str, a='x'), defaultdict(int, {'a': 1}), defaultdict(float), defaultdict(None))
print(defaultdict(int, [('a', 1), ('b', 2)]))

# The type and its relation to dict.
d = defaultdict(list)
print(type(d).__name__, type(d) is defaultdict, type(d) is dict, isinstance(d, defaultdict), isinstance(d, dict), isinstance({}, defaultdict))
print(issubclass(defaultdict, dict), issubclass(dict, defaultdict), defaultdict)
print(d.default_factory is list, defaultdict(None).default_factory)
d.default_factory = int
print(d['n'], d.default_factory is int)

# copies and merges keep the factory
c = counts.copy()
c['new'] += 1
print(type(c).__name__, c.default_factory is int, 'new' in counts, c['new'])
m = counts | {'zz': 1}
print(type(m).__name__, m.default_factory is int, m['zz'], m['unseen'])
print(type({**counts}).__name__, type(dict(counts)).__name__)
print(counts == dict(counts), dict(counts) == counts, counts == defaultdict(list, counts))
print(bool(defaultdict(int)), bool(counts))

t(lambda: defaultdict(1))
t(lambda: defaultdict(list)['x'])
t(lambda: defaultdict(None)['x'])
t(lambda: defaultdict(None, a=1)['b'])
t(lambda: defaultdict(list, 5))
t(lambda: defaultdict(list, {}, 3))
t(lambda: defaultdict(lambda: 1 // 0)['x'])
t(lambda: hash(defaultdict(int)))
del d['n']
t(lambda: d.pop('n'))
t(lambda: sorted(counts.items()))
