class Wrapper:
    def __init__(self, target):
        self.target = target
    def __getattr__(self, name):
        return getattr(self.target, name)
class Dog:
    def __init__(self): self.name = 'rex'
    def bark(self): return 'woof'
w = Wrapper(Dog())
print(w.name, w.bark())
try:
    w.nothing
except AttributeError as e:
    print('AttributeError')
