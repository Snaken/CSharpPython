class E(Exception):
    code = 500
    def describe(self):
        return 'E%d: %s' % (self.code, self) if False else 'E' + str(self.code) + ': ' + str(self)
class F(E):
    code = 404
try:
    raise F('nope')
except E as e:
    print(e.describe(), E.code, F.code)
