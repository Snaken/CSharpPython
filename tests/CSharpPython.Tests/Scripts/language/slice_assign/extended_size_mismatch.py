xs = list(range(6))
try:
    xs[::2] = [1, 2]
except ValueError as e:
    print('ValueError', e)
try:
    xs[::-2] = [1, 2, 3, 4]
except ValueError as e:
    print('ValueError', e)
print(xs)
try:
    xs[::0] = []
except ValueError as e:
    print('ValueError', e)
