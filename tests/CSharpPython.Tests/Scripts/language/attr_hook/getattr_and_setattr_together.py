class Record:
    def __init__(self):
        object.__setattr__(self, '_data', {})
    def __getattr__(self, name):
        try:
            return self._data[name]
        except KeyError:
            raise AttributeError(name)
    def __setattr__(self, name, value):
        self._data[name] = value
    def __delattr__(self, name):
        del self._data[name]
r = Record()
r.a = 1
r.b = 'two'
print(r.a, r.b, hasattr(r, 'c'))
del r.a
print(hasattr(r, 'a'), sorted(r._data))
