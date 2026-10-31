def f():
    def g():
        return y
    try:
        g()
    except NameError:
        print('NameError')
    y = 1
f()
