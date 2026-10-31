def make(n):
    class Local:
        def get(self):
            return n
    return Local
L = make(7)
print(L().get(), make(8)().get())
