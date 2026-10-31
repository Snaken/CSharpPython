def trace(label):
    print('make', label)
    def deco(f):
        print('wrap', label, f.__name__)
        def inner(*a, **k):
            print('call', label, inner.__name__)
            return f(*a, **k)
        return inner
    return deco
@trace('outer')
@trace('inner')
def f(x):
    return x + 1
print(f(1), f.__name__)
