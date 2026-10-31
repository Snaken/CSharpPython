try:
    issubclass(1, int)
except TypeError:
    print('TypeError')
try:
    issubclass(int, 1)
except TypeError:
    print('TypeError')
