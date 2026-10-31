class M:
    def a(self):
        return self.b() + 1
    def b(self):
        return 10
print(M().a())
