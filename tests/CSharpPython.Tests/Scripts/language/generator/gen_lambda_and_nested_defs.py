def outer():
    def inner():
        yield 'inner'
    yield from inner()
    f = lambda: 5
    yield f()
print(list(outer()))
def is_gen():
    return 1
    yield
print(type(is_gen()).__name__)
print(list(is_gen()))
