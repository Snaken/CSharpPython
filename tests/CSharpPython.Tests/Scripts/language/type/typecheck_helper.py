def check(v, t):
    if not isinstance(v, t):
        raise TypeError('expected ' + t.__name__ + ', got ' + type(v).__name__)
    return v
try:
    check('a', int)
except TypeError as e:
    print(e)
print(check(3, int))
