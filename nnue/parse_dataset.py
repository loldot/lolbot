#!/usr/bin/env python3
"""
Parse the balanced chess dataset and output FEN strings with evaluations.
Takes number of positions to parse as an argument.

Usage: python parse_dataset.py [num_positions]
"""

import sys
import argparse
import numpy as np
from typing import List, Tuple
import struct

class ChessRecord:
    """Represents a single chess position record."""
    def __init__(self, bitboards: np.ndarray, wdl: float, stm: int):
        self.bitboards = bitboards  # 8 uint64 bitboards
        self.wdl = wdl             # WDL value (0.0-1.0)
        self.stm = stm             # Side to move

def read_dataset_records(filename: str, num_positions: int) -> List[ChessRecord]:
    """Read specified number of records from the dataset."""
    
    print(f"📂 Reading {num_positions:,} records from {filename}")
    
    records = []
    
    try:
        with open(filename, 'rb') as f:
            for i in range(num_positions):
                # Each record is 73 bytes
                data = f.read(73)
                if len(data) != 73:
                    print(f"⚠️  Reached end of file after {i:,} records")
                    break
                
                # Parse bitboards (8 uint64s, bytes 0-63)
                bitboards = np.frombuffer(data[:64], dtype=np.uint64)
                
                # Parse WDL (float32, bytes 69-72)
                wdl_bytes = data[69:73]
                wdl = struct.unpack('<f', wdl_bytes)[0]
                
                # Parse STM (uint8, byte 68)
                stm = data[68]
                
                records.append(ChessRecord(bitboards, wdl, stm))
                
    except FileNotFoundError:
        print(f"❌ File not found: {filename}")
        return []
    except Exception as e:
        print(f"❌ Error reading file: {e}")
        return []
    
    print(f"✅ Successfully read {len(records):,} records")
    return records

def bitboard_to_square_list(bb: int) -> List[int]:
    """Convert a bitboard to a list of square indices (0-63)."""
    squares = []
    for i in range(64):
        if bb & (1 << i):
            squares.append(i)
    return squares

def square_to_algebraic(square: int) -> str:
    """Convert square index (0-63) to algebraic notation (a1-h8)."""
    file = square % 8
    rank = square // 8
    return chr(ord('a') + file) + str(rank + 1)

def bitboards_to_fen(bitboards: np.ndarray, stm: int) -> str:
    """Convert bitboards to FEN string."""
    
    # Extract individual bitboards
    bb_black = int(bitboards[0])
    bb_pawns = int(bitboards[1])
    bb_knights = int(bitboards[2])
    bb_bishops = int(bitboards[3])
    bb_rooks = int(bitboards[4])
    bb_queens = int(bitboards[5])
    bb_kings = int(bitboards[6])
    bb_white = int(bitboards[7])
    
    # Create 8x8 board representation
    board = [['.' for _ in range(8)] for _ in range(8)]
    
    # Helper function to place pieces
    def place_pieces(bb_piece: int, white_char: str, black_char: str):
        white_pieces = bb_piece & bb_white
        black_pieces = bb_piece & bb_black
        
        # Place white pieces
        for square in bitboard_to_square_list(white_pieces):
            rank = square // 8
            file = square % 8
            board[7-rank][file] = white_char  # FEN uses rank 8 at top
        
        # Place black pieces
        for square in bitboard_to_square_list(black_pieces):
            rank = square // 8
            file = square % 8
            board[7-rank][file] = black_char
    
    # Place all piece types
    place_pieces(bb_pawns, 'P', 'p')
    place_pieces(bb_knights, 'N', 'n')
    place_pieces(bb_bishops, 'B', 'b')
    place_pieces(bb_rooks, 'R', 'r')
    place_pieces(bb_queens, 'Q', 'q')
    place_pieces(bb_kings, 'K', 'k')
    
    # Convert board to FEN position part
    fen_rows = []
    for row in board:
        fen_row = ""
        empty_count = 0
        
        for cell in row:
            if cell == '.':
                empty_count += 1
            else:
                if empty_count > 0:
                    fen_row += str(empty_count)
                    empty_count = 0
                fen_row += cell
        
        if empty_count > 0:
            fen_row += str(empty_count)
        
        fen_rows.append(fen_row)
    
    position_part = '/'.join(fen_rows)
    
    # Determine side to move
    if stm == 0:
        side_to_move = 'b'
    elif stm == 7 or stm == 255:  # White (sometimes encoded as 255)
        side_to_move = 'w'
    else:
        side_to_move = 'w'  # Default to white for unknown values
    
    # For simplicity, we'll use default values for castling, en passant, etc.
    # since this information isn't stored in our bitboard format
    castling = 'KQkq'  # Assume all castling available
    en_passant = '-'   # No en passant info available
    halfmove = '0'     # Reset halfmove clock
    fullmove = '1'     # Default to move 1
    
    return f"{position_part} {side_to_move} {castling} {en_passant} {halfmove} {fullmove}"

def wdl_to_centipawns(wdl: float) -> int:
    """Convert WDL to centipawns approximation."""
    # Clamp WDL to reasonable range
    wdl = max(0.001, min(0.999, wdl))
    
    # Convert to centipawns using logistic approximation
    # WDL 0.5 = 0cp, WDL 0.9 ≈ +400cp, WDL 0.1 ≈ -400cp
    if wdl == 0.5:
        return 0
    
    # Use logistic transformation: cp = 400 * ln(wdl / (1 - wdl))
    import math
    try:
        cp = 400.0 * math.log(wdl / (1.0 - wdl))
        return int(round(cp))
    except (ValueError, ZeroDivisionError):
        # Fallback for extreme values
        return 800 if wdl > 0.5 else -800

def parse_and_output(filename: str, num_positions: int, output_format: str = 'both'):
    """Parse dataset and output FEN strings with evaluations."""
    
    records = read_dataset_records(filename, num_positions)
    
    if not records:
        print("❌ No records to process")
        return
    
    print(f"\n📋 Outputting {len(records):,} positions:")
    print("=" * 80)
    
    for i, record in enumerate(records, 1):
        try:
            # Convert to FEN
            fen = bitboards_to_fen(record.bitboards, record.stm)
            
            # Calculate centipawns
            centipawns = wdl_to_centipawns(record.wdl)
            
            # Output based on format
            if output_format == 'fen':
                print(f"{fen}")
            elif output_format == 'eval':
                print(f"{fen} | {centipawns:+d}cp")
            elif output_format == 'wdl':
                print(f"{fen} | WDL: {record.wdl:.6f}")
            else:  # both
                print(f"{fen} | {centipawns:+d}cp | WDL: {record.wdl:.6f}")
            
        except Exception as e:
            print(f"❌ Error processing record {i}: {e}")
            continue

def main():
    parser = argparse.ArgumentParser(description="Parse chess dataset and output FEN strings with evaluations")
    parser.add_argument("num_positions", type=int, nargs='?', default=10,
                       help="Number of positions to parse (default: 10)")
    parser.add_argument("--dataset", "-d", default="balanced_chess_data.bin",
                       help="Dataset filename (default: balanced_chess_data.bin)")
    parser.add_argument("--format", "-f", choices=['fen', 'eval', 'wdl', 'both'], default='both',
                       help="Output format: fen, eval, wdl, or both (default: both)")
    
    args = parser.parse_args()
    
    if args.num_positions <= 0:
        print("❌ Number of positions must be positive")
        sys.exit(1)
    
    print(f"🎯 CHESS DATASET PARSER")
    print(f"Dataset: {args.dataset}")
    print(f"Positions: {args.num_positions:,}")
    print(f"Format: {args.format}")
    
    parse_and_output(args.dataset, args.num_positions, args.format)

if __name__ == "__main__":
    main()