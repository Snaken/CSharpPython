class CM:
    def __enter__(self):
        return self
    def __exit__(self, t, v, tb):
        raise RuntimeError('from exit')
try:
    with CM():
        print('body')
except RuntimeError as e:
    print('caught', e)
try:
    with CM():
        raise ValueError('body error')
except RuntimeError as e:
    print('caught', e)
