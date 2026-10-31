class Account:
    rate = 0.5
    def __init__(self, owner, balance=0):
        self.owner = owner
        self.balance = balance
    def deposit(self, n):
        if n <= 0:
            raise ValueError('bad amount')
        self.balance += n
        return self.balance
class Savings(Account):
    def add_interest(self):
        self.balance += self.balance * Account.rate
a = Savings('ann', 100)
a.deposit(50)
a.add_interest()
print(a.owner, a.balance)
try:
    a.deposit(-1)
except ValueError as e:
    print(e)
