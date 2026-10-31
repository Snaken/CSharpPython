class A(object):
    def f(self):
        return 1
print(A().f(), isinstance(A(), object), issubclass(A, object))
