try:
    type()
except TypeError:
    print('TypeError')
try:
    type(1, 2)
except TypeError:
    print('TypeError')
