def total(*xs):
    if not xs:
        return 0
    return xs[0] + total(*xs[1:])
print(total(1, 2, 3, 4))
