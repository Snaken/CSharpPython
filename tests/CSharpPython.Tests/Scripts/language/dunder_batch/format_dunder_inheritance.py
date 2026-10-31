class A:
    def __format__(self, spec): return 'A<' + spec + '>'
class B(A): pass
print(f'{B():x}', format(B(), ''))
