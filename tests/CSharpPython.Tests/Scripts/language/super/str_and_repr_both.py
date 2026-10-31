class P:
    def __str__(self):
        return 'str'
    def __repr__(self):
        return 'repr'
p = P()
print(p, repr(p), str(p), [p])
print(str([p]), repr([p]))
