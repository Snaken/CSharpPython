def k(x):
    try:
        return x
    finally:
        print('k', x)
print(sorted([2,1], key=k))
