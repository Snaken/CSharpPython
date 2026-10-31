x = 1
y = 2
del x
print(y)
try:
    print(x)
except NameError as e:
    print('NameError', e)
x = 5
print(x)
del x, y
try:
    del y
except NameError as e:
    print('NameError', e)
