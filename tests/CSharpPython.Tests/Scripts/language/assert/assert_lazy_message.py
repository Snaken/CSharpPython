calls = []
def message():
    calls.append(1)
    return 'built'
assert True, message()
print(len(calls))
try:
    assert False, message()
except AssertionError as e:
    print(e, len(calls))
