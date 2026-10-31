g = []
def add(v):
    g.append(v)
    return v
r = [add(x) for x in range(3)]
print(r, g)
