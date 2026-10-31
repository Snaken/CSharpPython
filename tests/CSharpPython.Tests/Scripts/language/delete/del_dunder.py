class Bag:
    def __init__(self):
        self.d = {'a': 1, 'b': 2}
    def __delitem__(self, k):
        print('deleting', k)
        del self.d[k]
    def __getitem__(self, k):
        return self.d[k]
    def __len__(self):
        return len(self.d)
b = Bag()
del b['a']
print(len(b))
try:
    del b['zz']
except KeyError as e:
    print('KeyError', e)
class Plain:
    pass
try:
    del Plain()[0]
except TypeError as e:
    print('TypeError', e)
class Slices:
    def __delitem__(self, k):
        print(type(k).__name__, k.start, k.stop, k.step)
del Slices()[1:3]
del Slices()[::2]
