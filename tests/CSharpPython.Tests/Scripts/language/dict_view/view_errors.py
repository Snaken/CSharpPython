def t(f):
    try:
        print(f())
    except Exception as e:
        print(type(e).__name__, e)
d = {'a': 1}
t(lambda: d.keys()[0])
t(lambda: d.values()[0])
t(lambda: hash(d.keys()))
t(lambda: d.values() | d.values())
t(lambda: d.values() < d.values())
t(lambda: d.keys() | 5)
t(lambda: 5 - d.keys())
t(lambda: d.keys() + d.keys())
t(lambda: d.keys() < [1])
def grow():
    for k in d.keys():
        d['x' + k] = 1
t(grow)
def grow2():
    for k, v in d.items():
        d[k] = v + 1
    return d
t(grow2)
t(lambda: next(d.keys()))
t(lambda: len(5))
