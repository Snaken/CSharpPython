class A:
    def f(self, acc=[]):
        acc.append(1)
        return len(acc)
a = A()
print(a.f(), a.f(), A().f())
