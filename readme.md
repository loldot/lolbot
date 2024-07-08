# TODO:

- [x] End game on check mate
- [x] Pawn promotions
- [x] Pawns jumping over piece on first move
- [x] Pawns can check
- [x] En passant should capture
- [x] Checks
- [x] King cannot move away from check in check direction
- [x] Double Checks
- [x] Pins
- [x] Make king moves legal again
- [x] Display castling as legal move
- [x] Remove castling rights
- [ ] Frontend: Update board correctly when castling
- [x] SignalR


Split Pawn Bitboard into attacks and pushes
Make attacksbitboard

# Square mapping:

```
+---------------------------------------+
| 56 | 57 | 58 | 59 | 60 | 61 | 62 | 63 |
+----+----+----+----+----+----+----+----+
| 48 | 49 | 50 | 51 | 52 | 53 | 54 | 55 |
+----+----+----+----+----+----+----+----+
| 40 | 41 | 42 | 43 | 44 | 45 | 46 | 47 |
+----+----+----+----+----+----+----+----+
| 32 | 33 | 34 | 35 | 36 | 37 | 38 | 39 |
+----+----+----+----+----+----+----+----+
| 24 | 25 | 26 | 27 | 28 | 29 | 30 | 31 |
+----+----+----+----+----+----+----+----+
| 16 | 17 | 18 | 19 | 20 | 21 | 22 | 23 |
+----+----+----+----+----+----+----+----+
|  8 |  9 | 10 | 11 | 12 | 13 | 14 | 15 |
+----+----+----+----+----+----+----+----+
|  0 |  1 |  2 |  3 |  4 |  5 |  6 |  7 |
+----+----+----+----+----+----+----+----+
```