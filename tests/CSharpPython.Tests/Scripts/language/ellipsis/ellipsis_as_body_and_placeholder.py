def f():
    ...
class A:
    ...
def g(x):
    if x:
        ...
    else:
        return 1
print(f(), g(0), g(1), A().__class__.__name__)
