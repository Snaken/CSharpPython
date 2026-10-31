print('%(a)s-%(b)d' % {'a': 'x', 'b': 3})
print('%(n)5.1f|%(n)-8.2f|' % {'n': 2.25})
print('%(a)s %%' % {'a': 1})
print('%s' % {'a': 1})
print('%(x)s' % {'x': (1, 2)})
class M:
    def __getitem__(self, k): return k.upper()
print('%(ab)s %(cd)s' % M())
