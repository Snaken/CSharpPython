class E(Exception):
    def __init__(self, code):
        self.code = code
try:
    raise E(404)
except E as e:
    print(e.code, e.args, str(e), repr(e))
