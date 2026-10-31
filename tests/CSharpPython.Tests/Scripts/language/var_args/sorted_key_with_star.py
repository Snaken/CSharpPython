def k(*a):
    return -a[0]
print(sorted([1, 3, 2], key=k))
