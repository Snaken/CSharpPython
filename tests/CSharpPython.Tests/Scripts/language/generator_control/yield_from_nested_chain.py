def a():
    v = yield 'a1'
    print('a got', v)
    return 'a-result'
def b():
    r = yield from a()
    w = yield 'b1'
    print('b got', w)
    return r + '/b'
def c():
    r = yield from b()
    print('c result', r)
it = c()
print(next(it))
print(it.send(1))
try:
    it.send(2)
except StopIteration:
    pass
