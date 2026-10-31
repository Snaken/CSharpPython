def inner():
    got = []
    while len(got) < 3:
        try:
            got.append((yield len(got)))
        except ValueError:
            got.append('V')
    return got
def outer():
    r = yield from inner()
    print('result', r)
g = outer()
print(next(g))
print(g.send('a'))
print(g.throw(ValueError))
try:
    g.send('c')
except StopIteration:
    print('done')
