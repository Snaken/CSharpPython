class A:
    def __bool__(self):
        return False
    def __len__(self):
        return 5
print(bool(A()), len(A()))
