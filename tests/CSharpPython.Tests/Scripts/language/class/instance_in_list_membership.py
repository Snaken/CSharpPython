class P: pass
a = P()
b = P()
xs = [a]
print(a in xs, b in xs, xs.index(a))
