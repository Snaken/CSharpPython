def outer(n):
    def even(k):
        return True if k == 0 else odd(k - 1)
    def odd(k):
        return False if k == 0 else even(k - 1)
    return even(n)
print(outer(10), outer(7))
