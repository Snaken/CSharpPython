def outer(*a):
    def inner(*b):
        return a + b
    return inner(9)
print(outer(1, 2))
