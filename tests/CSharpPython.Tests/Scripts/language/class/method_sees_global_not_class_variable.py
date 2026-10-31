x = 'global'
class K:
    x = 'class'
    def f(self):
        return x
    def g(self):
        return self.x
print(K().f(), K().g(), K.x)
