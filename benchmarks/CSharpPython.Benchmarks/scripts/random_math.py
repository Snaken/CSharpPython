import math
import random


def main():
    random.seed(12345)
    total = 0.0
    for i in range(50000):
        total += random.random() + random.randint(1, 6) + math.sqrt(i) + math.floor(random.uniform(0, 10))
    assert total > 0
    items = list(range(1000))
    for i in range(50):
        random.shuffle(items)
    assert sorted(items) == list(range(1000))
    assert len(random.sample(items, 100)) == 100

main()
