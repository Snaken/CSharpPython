s = {frozenset([1, 2])}
print({1, 2} in s, frozenset([2, 1]) in s, {3} in s)
s.discard({1, 2})
print(len(s))
