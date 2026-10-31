class P:
    count: int = 0
    name: str
    def __init__(self, x: int, y: int = 0) -> None:
        self.x: int = x
        self.y: int
        self.y = y
    def total(self) -> int:
        return self.x + self.y
p = P(2, 3)
print(p.total(), P.count, p.x, p.y)
