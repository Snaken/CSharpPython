| Script | CPython 3.14.7 | .NET 10 (JIT) | Unity 7 IL2CPP | Unity 6.3 IL2CPP | Unity 6.3 Mono |
| --- | ---: | ---: | ---: | ---: | ---: |
| builtins_stream | 63.6 ms | 64.8 ms (1.0x) | 114.1 ms (1.8x) | 115.3 ms (1.8x) | 369.7 ms (5.8x) |
| classes | 5.8 ms | 11.3 ms (1.9x) | 19.5 ms (3.3x) | 25.0 ms (4.3x) | 61.7 ms (10.6x) |
| collections_ops | 11.0 ms | 24.4 ms (2.2x) | 29.9 ms (2.7x) | 34.6 ms (3.2x) | 80.4 ms (7.3x) |
| dict_ops | 3.0 ms | 5.5 ms (1.8x) | 9.0 ms (3.0x) | 9.0 ms (2.9x) | 26.9 ms (8.8x) |
| fib | 1.9 ms | 4.0 ms (2.1x) | 5.2 ms (2.7x) | 5.3 ms (2.8x) | 14.1 ms (7.4x) |
| generators | 3.4 ms | 8.8 ms (2.6x) | 13.1 ms (3.9x) | 13.7 ms (4.0x) | 37.2 ms (10.9x) |
| list_ops | 5.0 ms | 8.9 ms (1.8x) | 12.0 ms (2.4x) | 12.1 ms (2.4x) | 40.4 ms (8.0x) |
| loop_sum | 17.0 ms | 27.8 ms (1.6x) | 43.7 ms (2.6x) | 47.9 ms (2.8x) | 170.6 ms (10.0x) |
| random_math | 11.7 ms | 10.2 ms (0.9x) | 15.3 ms (1.3x) | 18.4 ms (1.6x) | 59.3 ms (5.1x) |
| strings | 1.9 ms | 2.2 ms (1.1x) | 4.8 ms (2.5x) | 7.1 ms (3.7x) | 13.0 ms (6.8x) |
