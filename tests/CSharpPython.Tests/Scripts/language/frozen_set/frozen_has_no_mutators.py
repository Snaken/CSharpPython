a = frozenset([1])
for name in ('add', 'remove', 'discard', 'pop', 'clear', 'update', 'intersection_update', 'difference_update', 'symmetric_difference_update'):
    print(name, hasattr(a, name))
try:
    a.add(2)
except AttributeError:
    print('AttributeError')
