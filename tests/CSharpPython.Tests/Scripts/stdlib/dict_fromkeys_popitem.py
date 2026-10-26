def t(f):
    try:
        r = f()
        print(type(r).__name__, repr(r))
    except Exception as e:
        print(type(e).__name__)
t(lambda: dict.fromkeys([1, 2, 3]))
t(lambda: dict.fromkeys('abc', 0))
t(lambda: dict.fromkeys([]))
t(lambda: dict.fromkeys([1, 1, 2], 'x'))
t(lambda: dict.fromkeys(range(3), []))
t(lambda: {}.fromkeys([1, 2], 9))
t(lambda: {'z': 1}.fromkeys(['a']))
t(lambda: dict.fromkeys([[1]]))
t(lambda: dict.fromkeys(5))
t(lambda: dict.fromkeys())
t(lambda: dict.fromkeys([1], 2, 3))
d = dict.fromkeys(range(3), [])
d[0].append(1)
print(d)
d = {1: 'a', 2: 'b', 3: 'c'}
print(d.popitem(), d, d.popitem(), d.popitem(), d)
t(lambda: d.popitem())
d = {'a': 1}
d['b'] = 2
d['a'] = 3
print(d.popitem(), d)
d = {1: 1, 2: 2}
del d[2]
d[2] = 5
print(d.popitem(), d.popitem(), d)
t(lambda: {}.popitem(1))
