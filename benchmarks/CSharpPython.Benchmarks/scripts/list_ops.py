def main():
    items = []
    for i in range(100000):
        items.append(i * 3 % 1001)
    items.sort()
    squares = [x * x for x in items if x % 2 == 0]
    assert len(squares) > 0
    assert sum(squares) > 0

main()
