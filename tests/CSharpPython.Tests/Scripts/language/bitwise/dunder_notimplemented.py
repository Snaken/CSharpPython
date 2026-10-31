class A:
    def __or__(self, o):
        return NotImplemented
    def __ror__(self, o):
        return 'A.ror'
class B:
    def __or__(self, o):
        return NotImplemented
print(1 | A(), B() | A())
try:
    B() | B()
except TypeError as e:
    print('TypeError', e)
class Base:
    def __and__(self, o):
        return 'base'
class Sub(Base):
    def __rand__(self, o):
        return 'sub-rand'
print(Base() & Sub(), Sub() & Base(), Sub() & Sub())
