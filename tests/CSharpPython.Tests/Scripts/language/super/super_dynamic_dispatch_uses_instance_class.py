class A:
    def run(self):
        return self.step()
    def step(self):
        return 'A'
class B(A):
    def step(self):
        return 'B' + super().step()
print(B().run())
