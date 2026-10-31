a = [10, 20, 30, 40, 50]
del a[0]
print(a)
del a[-1]
print(a)
del a[1]
print(a)
try:
    del a[5]
except IndexError as e:
    print('IndexError', e)
try:
    del a['x']
except TypeError as e:
    print('TypeError', e)
del a[0], a[0]
print(a)
