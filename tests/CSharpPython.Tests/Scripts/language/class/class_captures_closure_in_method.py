def outer(k):
    class Inner:
        def mul(self, x):
            return x * k
    return Inner()
print(outer(3).mul(4))
