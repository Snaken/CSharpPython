try:
    int('a')
except ValueError:
    print('ValueError')
try:
    int(1, 2, 3)
except TypeError:
    print('TypeError')
try:
    len()
except TypeError:
    print('TypeError')
