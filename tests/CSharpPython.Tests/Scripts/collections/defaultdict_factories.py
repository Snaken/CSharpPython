from collections import defaultdict

calls = []

def make():
    calls.append('made')
    return [0]

d = defaultdict(make)
d['a'].append(1)
d['a'].append(2)
d['b']
print(dict(d), calls)
x = d['a']
print(calls)
d['c'] += [7]
print(d['c'], len(calls))

class Box:
    def __init__(self):
        self.items = []

boxes = defaultdict(Box)
boxes['x'].items.append(1)
boxes['x'].items.append(2)
boxes['y']
print({k: v.items for k, v in boxes.items()})

# A factory that returns None still stores it.
n = defaultdict(lambda: None)
print(n['a'], 'a' in n, dict(n))

# Item assignment, deletion and iteration behave like a dict.
d = defaultdict(int)
d['a'] = 1
d['b'] = 2
del d['a']
print(d, list(d.keys()), list(d.values()), sum(d.values()))
for k in list(d):
    d[k] *= 10
print(d)
d.update({'c': 3}, e=4)
print(d)
print(sorted(d.items(), key=lambda kv: -kv[1]))
print(d.popitem(), d)
print({k: v for k, v in d.items() if v > 20})
