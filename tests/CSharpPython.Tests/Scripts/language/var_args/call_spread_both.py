def f(a, b, c=0, d=0):
    return a, b, c, d
print(f(*[1, 2], **{'d': 4}))
