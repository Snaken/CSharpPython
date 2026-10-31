def k(x):
    try:
        return 10 // x
    except ZeroDivisionError:
        return 0
print(sorted([5,0,2], key=k))
