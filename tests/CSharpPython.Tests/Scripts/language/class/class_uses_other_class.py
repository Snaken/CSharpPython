class Engine:
    def __init__(self):
        self.on = False
    def start(self):
        self.on = True
class Car:
    def __init__(self):
        self.engine = Engine()
    def drive(self):
        self.engine.start()
        return self.engine.on
print(Car().drive())
