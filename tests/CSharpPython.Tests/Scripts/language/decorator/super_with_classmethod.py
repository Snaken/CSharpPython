class A:
    @classmethod
    def make(cls):
        return 'A.make ' + cls.__name__
    @staticmethod
    def util(): return 'util'
class B(A):
    @classmethod
    def make(cls):
        return super().make() + ' via B'
    def u(self):
        return super().util()
print(B.make(), B().make(), B().u())
