def g():
    for i in range(3):
        yield i
    else:
        yield 'else'
print(list(g()))
def h():
    for i in range(3):
        if i == 1:
            break
        yield i
    else:
        yield 'else'
    yield 'end'
print(list(h()))
