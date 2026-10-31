class CM:
    def __enter__(self):
        return 1
    def __exit__(self, *a):
        print('exit')
x = 'before'
with CM() as x:
    pass
print(x)
def f():
    with CM() as y:
        y += 10
        return y
print(f())
