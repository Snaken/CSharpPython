class CM:
    def __enter__(self):
        print('enter')
        raise ValueError('enter failed')
    def __exit__(self, t, v, tb):
        print('exit should not run')
try:
    with CM():
        print('body')
except ValueError as e:
    print('caught', e)
class Second:
    def __enter__(self):
        raise KeyError('second')
    def __exit__(self, t, v, tb):
        print('second exit')
class First:
    def __enter__(self):
        return self
    def __exit__(self, t, v, tb):
        print('first exit', t.__name__)
try:
    with First(), Second():
        pass
except KeyError:
    print('caught key')
