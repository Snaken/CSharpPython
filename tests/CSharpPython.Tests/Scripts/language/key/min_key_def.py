def dist(p):
    return abs(p[0]) + abs(p[1])
print(min([(3,4),(1,1),(-2,0)], key=dist))
