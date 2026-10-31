class E(Exception):
    def __init__(self, v):
        self.v = v
try:
    try:
        raise E(5)
    except E as e:
        raise
except E as e2:
    print(e2.v)
