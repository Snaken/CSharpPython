xs = [1, 2]
for bad in (5, 1.5, None, True):
    try:
        xs[0:1] = bad
    except TypeError as e:
        print('TypeError')
print(xs)
