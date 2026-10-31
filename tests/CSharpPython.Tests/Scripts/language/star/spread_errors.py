try:
    x = 5
    print([*x])
except TypeError as e:
    print('TypeError')
try:
    print({**[1, 2]})
except TypeError as e:
    print('TypeError')
try:
    print({**None})
except TypeError as e:
    print('TypeError')
try:
    print({*5})
except TypeError as e:
    print('TypeError')
