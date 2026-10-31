class CM:
    def __init__(self, name):
        self.name = name
    def __enter__(self):
        print('enter', self.name)
        return self.name
    def __exit__(self, t, v, tb):
        print('exit', self.name)
        return False
with CM('a') as x, CM('b') as y, CM('c'):
    print(x, y)
try:
    with CM('a'), CM('b'):
        raise RuntimeError('inner')
except RuntimeError as e:
    print('caught', e)
