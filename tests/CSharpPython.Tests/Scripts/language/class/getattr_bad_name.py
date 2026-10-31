try:
    getattr(1, 2)
except TypeError:
    print('TypeError')
try:
    hasattr(1, 2)
except TypeError:
    print('TypeError')
try:
    setattr(1, 2, 3)
except TypeError:
    print('TypeError')
