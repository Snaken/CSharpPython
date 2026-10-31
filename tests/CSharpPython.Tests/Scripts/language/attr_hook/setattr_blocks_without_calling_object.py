class Sink:
    def __setattr__(self, name, value):
        pass
s = Sink()
s.x = 1
print(hasattr(s, 'x'))
