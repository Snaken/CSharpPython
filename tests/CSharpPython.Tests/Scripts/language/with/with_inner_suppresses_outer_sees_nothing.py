class CM:
    def __init__(self, name, swallow):
        self.name = name
        self.swallow = swallow
    def __enter__(self):
        return self
    def __exit__(self, t, v, tb):
        print(self.name, 'sees', t.__name__ if t else None)
        return self.swallow
with CM('outer', False), CM('inner', True):
    raise ValueError('v')
print('done')
