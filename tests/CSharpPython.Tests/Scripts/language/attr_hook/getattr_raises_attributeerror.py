class A:
    def __getattr__(self, name):
        if name.startswith('x'):
            return 42
        raise AttributeError('no ' + name)
a = A()
print(a.xyz)
try:
    a.abc
except AttributeError as e:
    print('AttributeError', e)
print(hasattr(a, 'abc'), hasattr(a, 'xq'), getattr(a, 'abc', 'dflt'))
