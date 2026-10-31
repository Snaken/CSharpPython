class MyErr(Exception):
    pass
def g():
    try:
        yield 1
    except MyErr as e:
        print('MyErr', e.args)
        yield 2
it = g()
next(it)
print(it.throw(MyErr, 'x'))
it = g()
next(it)
print(it.throw(MyErr('y')))
