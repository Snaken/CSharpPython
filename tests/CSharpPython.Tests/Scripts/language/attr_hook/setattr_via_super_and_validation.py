class Strict:
    def __setattr__(self, name, value):
        if name.startswith('_'):
            raise AttributeError('private ' + name)
        super().__setattr__(name, value)
s = Strict()
s.ok = 1
print(s.ok)
try:
    s._hidden = 2
except AttributeError as e:
    print('AttributeError', e)
