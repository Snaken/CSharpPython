def f():
    pass
try:
    type(f)()
except TypeError:
    print('TypeError')
