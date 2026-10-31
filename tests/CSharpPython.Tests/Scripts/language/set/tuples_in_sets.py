s = {(1, 2), (1, 2), (2, 1)}
print(len(s), (1, 2) in s, (3, 4) in s)
seen = set()
for p in [(0, 0), (1, 0), (0, 0)]:
    if p in seen:
        print('dup', p)
    seen.add(p)
print(sorted(seen))
