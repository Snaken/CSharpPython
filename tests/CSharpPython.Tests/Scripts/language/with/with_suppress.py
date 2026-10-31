class Quiet:
    def __enter__(self):
        return self
    def __exit__(self, t, v, tb):
        print('suppressing', t.__name__)
        return True
with Quiet():
    raise KeyError('k')
    print('not reached')
print('continued')
with Quiet():
    1 / 0
print('still going')
