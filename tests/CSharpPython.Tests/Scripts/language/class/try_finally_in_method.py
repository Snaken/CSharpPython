class A:
    def f(self):
        try:
            return 1
        finally:
            print('cleanup')
print(A().f())
