def f(*args, sep=' '):
    return sep.join([str(x) for x in args])
print(f(1, 2, 3), f(1, 2, sep='-'))
