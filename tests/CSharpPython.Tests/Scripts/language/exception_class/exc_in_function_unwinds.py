class Dead(Exception):
    def __init__(self, who):
        super().__init__(who + ' died')
        self.who = who
def attack(x):
    if x > 2:
        raise Dead('orc')
    return x
total = 0
try:
    for i in range(10):
        total += attack(i)
except Dead as d:
    print(d, d.who, total)
