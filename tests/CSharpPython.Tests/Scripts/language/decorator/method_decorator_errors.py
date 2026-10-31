def bad(x):
    return x
try:
    @bad
    def f(): pass
    @undefined_decorator
    def g(): pass
except NameError as e:
    print('NameError')
try:
    @(1)
    def h(): pass
except TypeError as e:
    print('TypeError')
