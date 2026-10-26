def main():
    parts = []
    for i in range(20000):
        parts.append(f'item {i}: {i * 2}')
    text = ','.join(parts)
    assert len(text.split(',')) == 20000
    assert text.upper().count('ITEM') == 20000

main()
