def f(*, a=1, b, c=3):
    return a, b, c
print(f(b=2))
