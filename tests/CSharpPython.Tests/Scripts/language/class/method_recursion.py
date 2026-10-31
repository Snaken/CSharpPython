class M:
    def fact(self, n):
        return 1 if n <= 1 else n * self.fact(n - 1)
print(M().fact(10))
