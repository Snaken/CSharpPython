class K:
    pass
a = K()
d = {a: 1}
print(d[a], K() in d, hash(a) == hash(a))
