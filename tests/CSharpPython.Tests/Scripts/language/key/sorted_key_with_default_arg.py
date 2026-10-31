def f(x, scale=2):
    return -x * scale
print(sorted([1,3,2], key=f))
