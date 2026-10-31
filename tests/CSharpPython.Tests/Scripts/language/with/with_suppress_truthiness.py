class R:
    def __init__(self, r):
        self.r = r
    def __enter__(self):
        return self
    def __exit__(self, t, v, tb):
        return self.r
for r in (1, 'yes', [0], None, 0, '', []):
    try:
        with R(r):
            raise ValueError('x')
        print('suppressed', repr(r))
    except ValueError:
        print('propagated', repr(r))
