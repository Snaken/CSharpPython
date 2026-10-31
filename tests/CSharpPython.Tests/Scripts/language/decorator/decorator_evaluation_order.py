def d(n):
    print('eval', n)
    def inner(f):
        print('apply', n)
        return f
    return inner
@d(1)
@d(2)
@d(3)
def f():
    print('body')
print('defined')
f()
