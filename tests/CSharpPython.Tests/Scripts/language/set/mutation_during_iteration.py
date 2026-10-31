s = {1, 2, 3}
try:
    for x in s:
        s.add(x + 10)
except RuntimeError as e:
    print('RuntimeError', e)
s = {1, 2, 3}
for x in list(s):
    s.discard(x)
print(s)
