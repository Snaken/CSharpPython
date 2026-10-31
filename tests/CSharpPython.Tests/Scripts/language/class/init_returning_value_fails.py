class V:
    def __init__(self):
        return 5
try:
    V()
except TypeError:
    print('TypeError')
