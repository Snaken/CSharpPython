def f():
    yield 1
    return 'ignored'
    yield 2
print(list(f()))
def g():
    return
    yield
print(list(g()))
def h(flag):
    if flag:
        return
    yield 'ran'
print(list(h(True)), list(h(False)))
