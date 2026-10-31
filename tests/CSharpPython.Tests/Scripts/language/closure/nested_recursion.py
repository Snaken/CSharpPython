def outer(n):
    def fact(k):
        return 1 if k <= 1 else k * fact(k - 1)
    return fact(n)
print(outer(10))
