s = set()
s.add(1)
s.add(2)
s.add(1)
print(sorted(s), len(s))
s.discard(5)
s.discard(1)
print(s)
s.remove(2)
print(s, len(s))
try:
    s.remove(2)
except KeyError as e:
    print('KeyError', e)
try:
    s.pop()
except KeyError as e:
    print('KeyError', e)
s.update([5, 6], (7,), 'x')
print(sorted(s, key=str))
print(s.pop() in {5, 6, 7, 'x'}, len(s))
s.clear()
print(s, len(s))
