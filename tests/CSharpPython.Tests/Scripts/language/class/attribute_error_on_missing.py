class A:
    def __init__(self):
        self.x = 1
a = A()
try:
    a.y
except AttributeError:
    print('missing y')
a.y = 2
print(a.y)
