class CM:
    def __enter__(self):
        print('enter')
        return 'v'
    def __exit__(self, *a):
        print('exit')
        return False
def f():
    with CM() as v:
        yield v
        yield 'second'
    yield 'after'
print(list(f()))
