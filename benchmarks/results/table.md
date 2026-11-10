| Script | CPython 3.14.7 | .NET 10 (JIT) | Unity 7 IL2CPP | Unity 6.3 IL2CPP | Unity 6.3 Mono |
| --- | ---: | ---: | ---: | ---: | ---: |
| builtins_stream | 64.3 ms | 63.2 ms (1.0x) | 113.8 ms (1.8x) | 115.9 ms (1.8x) | 365.9 ms (5.7x) |
| classes | 6.0 ms | 15.8 ms (2.6x) | 34.0 ms (5.7x) | 50.8 ms (8.5x) | 111.2 ms (18.6x) |
| collections_ops | 11.0 ms | 25.1 ms (2.3x) | 30.1 ms (2.7x) | 35.5 ms (3.2x) | 86.0 ms (7.8x) |
| dict_ops | 3.1 ms | 5.1 ms (1.6x) | 8.8 ms (2.8x) | 9.1 ms (2.9x) | 27.1 ms (8.7x) |
| fib | 1.9 ms | 4.8 ms (2.5x) | 6.5 ms (3.3x) | 8.1 ms (4.2x) | 20.4 ms (10.5x) |
| generators | 3.5 ms | 9.4 ms (2.7x) | 14.6 ms (4.1x) | 15.4 ms (4.4x) | 44.9 ms (12.7x) |
| list_ops | 5.3 ms | 9.4 ms (1.8x) | 14.8 ms (2.8x) | 15.3 ms (2.9x) | 43.8 ms (8.3x) |
| loop_sum | 17.2 ms | 26.9 ms (1.6x) | 45.3 ms (2.6x) | 47.7 ms (2.8x) | 169.4 ms (9.8x) |
| random_math | 11.7 ms | 11.5 ms (1.0x) | 16.7 ms (1.4x) | 20.7 ms (1.8x) | 70.2 ms (6.0x) |
| strings | 2.0 ms | 2.1 ms (1.0x) | 4.8 ms (2.4x) | 7.1 ms (3.5x) | 13.3 ms (6.5x) |
