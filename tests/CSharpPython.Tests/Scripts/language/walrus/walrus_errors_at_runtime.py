def f():
    return u
try:
    f()
except NameError:
    print('NameError')
try:
    if (q := 1 / 0):
        pass
except ZeroDivisionError:
    print('ZeroDivisionError')
try:
    q
except NameError:
    print('q unbound')
