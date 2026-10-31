print(sorted(set([3, 1, 3])), sorted(set('hello')), sorted(set((1, 2, 2))), sorted(set({'a': 1, 'b': 2})), sorted(set(range(4))))
a = [1, 2]
s = set(a)
s.add(3)
print(a, sorted(s))
t = set(s)
t.add(9)
print(sorted(s), sorted(t))
try:
    set(5)
except TypeError as e:
    print('TypeError', e)
try:
    set([[1]])
except TypeError:
    print('TypeError')
