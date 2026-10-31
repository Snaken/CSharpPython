class A:
    count = 0
    def add(self):
        A.count += 1
class B(A):
    def add(self):
        super().add()
        super().add()
B().add()
print(A.count)
