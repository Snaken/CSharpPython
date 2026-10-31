d = {'a': 1}
k, v, i = d.keys(), d.values(), d.items()
d['b'] = 2
d['a'] = 10
print(k, v, i, len(k))
del d['a']
print(k, v, i)
d.clear()
print(k, v, i, len(v))
