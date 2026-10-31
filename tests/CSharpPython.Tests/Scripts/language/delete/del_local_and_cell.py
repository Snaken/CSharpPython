def f():
    a = 1
    b = 2
    del a
    try:
        print(a)
    except UnboundLocalError as e:
        print('UnboundLocalError')
    a = 3
    del a, b
    try:
        del b
    except UnboundLocalError:
        print('UnboundLocalError again')
    return 'done'
print(f())
def g():
    x = 1
    def inner():
        return x
    del x
    try:
        inner()
    except NameError as e:
        print('NameError')
    x = 2
    return inner()
print(g())
def h():
    v = 10
    def inner():
        nonlocal v
        del v
    inner()
    try:
        return v
    except NameError:
        return 'gone'
print(h())
