try:
    isinstance(1, 'int')
except TypeError:
    print('TypeError')
try:
    isinstance(1, 5)
except TypeError:
    print('TypeError')
try:
    isinstance(1, [int])
except TypeError:
    print('TypeError')
