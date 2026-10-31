items = [1, 2, 3, 4, 5, 6]
i = 0
while i < len(items):
    if items[i] % 2 == 0:
        del items[i]
    else:
        i += 1
print(items)
d = {1: 'a', 2: 'b', 3: 'c'}
for k in list(d):
    if k != 2:
        del d[k]
print(d)
try:
    for k in d:
        del d[k]
except RuntimeError as e:
    print('RuntimeError', e)
