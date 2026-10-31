class A:
    def __repr__(self):
        return 'A!' + super().__repr__()[:10]
print(repr(A()))
