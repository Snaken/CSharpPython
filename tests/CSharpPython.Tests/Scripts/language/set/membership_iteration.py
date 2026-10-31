s = {10, 20, 30}
print(10 in s, 15 in s, 15 not in s, 1.0 in {1}, True in {1})
total = 0
for x in s:
    total += x
print(total, sum(s), min(s), max(s), len(s), any(s), all(s))
print(sorted(x * 2 for x in s), sorted(enumerate(sorted(s))), list(zip(sorted(s), 'abc')))
print(sorted([x for x in s if x > 10]), sorted(s, reverse=True))
a, b, c = sorted(s)
print(a, b, c)
print('x'.join(sorted(set('abca'))))
def f(*args):
    return len(args)
print(f(*{1, 2, 3}))
try:
    s[0]
except TypeError as e:
    print('TypeError', e)
