def add_hello(cls):
    cls.hello = lambda self: 'hello ' + self.name
    return cls
@add_hello
class P:
    def __init__(self, name): self.name = name
print(P('bob').hello())
def rename(cls):
    return 7
@rename
class Q: pass
print(Q)
registry = {}
def reg(name):
    def d(cls):
        registry[name] = cls
        return cls
    return d
@reg('a')
@reg('b')
class R: pass
print(sorted(registry), registry['a'] is R)
