f = lambda: (t := 5)
print(f())
def gen():
    while (n := (yield 1)) is None:
        pass
    yield n
it = gen()
print(next(it), it.send(9))
print(list((s := i * 2) for i in range(3)), s)
