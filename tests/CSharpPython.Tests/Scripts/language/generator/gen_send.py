def accumulate():
    total = 0
    while True:
        x = yield total
        if x is None:
            break
        total += x
    return total
g = accumulate()
print(next(g))
print(g.send(5))
print(g.send(10))
try:
    g.send(None)
except StopIteration:
    print('finished')
h = accumulate()
try:
    h.send(1)
except TypeError as e:
    print('TypeError', e)
print(h.send(None))
