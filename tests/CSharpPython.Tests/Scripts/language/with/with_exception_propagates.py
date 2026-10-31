class CM:
    def __enter__(self):
        return self
    def __exit__(self, t, v, tb):
        print('exit', t.__name__, v)
        return False
try:
    with CM():
        raise ValueError('boom')
except ValueError as e:
    print('caught', e)
