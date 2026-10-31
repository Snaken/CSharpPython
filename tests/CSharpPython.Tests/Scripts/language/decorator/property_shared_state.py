class Account:
    def __init__(self, balance):
        self._balance = balance
    @property
    def balance(self):
        return self._balance
    @balance.setter
    def balance(self, v):
        if v < 0:
            raise ValueError('negative')
        self._balance = v
    @property
    def doubled(self):
        return self.balance * 2
a = Account(10)
a.balance = 25
print(a.balance, a.doubled)
try:
    a.balance = -1
except ValueError:
    print(a.balance)
accounts = [Account(i) for i in range(3)]
print([x.doubled for x in accounts])
print(sum(x.balance for x in accounts))
