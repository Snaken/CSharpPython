def inner(a, b=2, *, c=3):
    return a, b, c
def wrap(*args, **kwargs):
    return inner(*args, **kwargs)
print(wrap(1), wrap(1, 5, c=7), wrap(a=9))
