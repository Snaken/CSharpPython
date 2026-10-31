log = []
def gen():
    log.append('start')
    yield 1
    yield 2
    log.append('end')
print([*gen(), *gen()])
print(log)
print({*gen()} == {1, 2})
a, *b = gen()
print(a, b, log)
