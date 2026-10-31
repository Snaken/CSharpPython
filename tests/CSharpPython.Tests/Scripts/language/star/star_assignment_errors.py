try:
    a, *b, c = [1]
except ValueError as e:
    print('ValueError', e)
try:
    a, *b = 5
except TypeError as e:
    print('TypeError', e)
try:
    a, b, *c = [1]
except ValueError as e:
    print('ValueError', e)
