class A:
    @property
    def p(self):
        raise AttributeError('inside')
    def __getattr__(self, name):
        return 'fallback ' + name
print(A().p)
