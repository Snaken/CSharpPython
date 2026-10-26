def main():
    total = 0
    for i in range(1000000):
        total += i * 2 % 7
    assert total == 2999997

main()
