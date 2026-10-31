class Left:
    def __eq__(self, other):
        return NotImplemented
class Right:
    def __eq__(self, other):
        return 'right says yes'
print(Left() == Right(), Right() == Left(), Left() == 3, 3 == Right())
