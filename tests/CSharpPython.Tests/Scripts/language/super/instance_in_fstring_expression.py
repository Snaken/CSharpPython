class A:
    def __str__(self):
        return 'x'
a = A()
print(f'{a} and {a!r:.3}'[:5])
