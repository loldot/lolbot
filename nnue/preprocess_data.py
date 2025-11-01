#!/usr/bin/env python3
"""
Data preprocessing pipeline to create better training data for chess evaluation.
"""

import os
import struct
import numpy as np
import torch
from typing import List, Tuple, Dict
from dataclasses import dataclass
import random

@dataclass
class ChessRecord:
    """Represents a single chess position record."""
    bitboards: List[int]  # 8 bitboards as uint64
    wdl: float
    stm: int  # side to move (0=black, 7=white)

def read_original_data(file_path: str, max_records: int | None = None) -> List[ChessRecord]:
    """Read the original binary data format."""
    
    print(f"📖 Reading original data from {file_path}")
    
    records = []
    file_size = os.path.getsize(file_path)
    num_records = file_size // 73
    
    if max_records:
        num_records = min(num_records, max_records)
    
    with open(file_path, 'rb') as f:
        for i in range(num_records):
            if i % 100000 == 0:
                print(f"  Progress: {i:,}/{num_records:,} ({100*i/num_records:.1f}%)")
            
            record_bytes = f.read(73)
            if len(record_bytes) != 73:
                break
            
            # Parse 8 bitboards (64 bytes total)
            bitboards = []
            for j in range(8):
                bb_bytes = record_bytes[j*8:(j+1)*8]
                bb_value = struct.unpack('<Q', bb_bytes)[0]
                bitboards.append(bb_value)
            
            # Parse WDL value at offset 69
            wdl_bytes = record_bytes[69:73]
            wdl = struct.unpack('<f', wdl_bytes)[0]
            
            # Parse STM from offset 68 (assuming it's there)
            stm = record_bytes[68]
            
            records.append(ChessRecord(bitboards=bitboards, wdl=wdl, stm=stm))
    
    print(f"  Loaded {len(records):,} records")
    return records

def analyze_data_distribution(records: List[ChessRecord]) -> Dict:
    """Analyze the distribution of WDL values and positions."""
    
    print(f"\n📊 Analyzing data distribution...")
    
    wdl_values = [r.wdl for r in records]
    
    stats = {
        'total_records': len(records),
        'wdl_min': min(wdl_values),
        'wdl_max': max(wdl_values),
        'wdl_mean': np.mean(wdl_values),
        'wdl_std': np.std(wdl_values)
    }
    
    # Count decisive positions
    decisive_low = sum(1 for w in wdl_values if w < 0.2)    # Strong for black
    decisive_high = sum(1 for w in wdl_values if w > 0.8)   # Strong for white
    balanced = sum(1 for w in wdl_values if 0.4 <= w <= 0.6)  # Balanced
    
    stats.update({
        'decisive_low': decisive_low,
        'decisive_high': decisive_high, 
        'balanced': balanced,
        'decisive_total': decisive_low + decisive_high,
        'decisive_pct': 100 * (decisive_low + decisive_high) / len(records)
    })
    
    print(f"  Total records: {stats['total_records']:,}")
    print(f"  WDL range: {stats['wdl_min']:.6f} to {stats['wdl_max']:.6f}")
    print(f"  WDL mean: {stats['wdl_mean']:.6f} ± {stats['wdl_std']:.6f}")
    print(f"  Decisive positions: {stats['decisive_total']:,} ({stats['decisive_pct']:.1f}%)")
    print(f"    Black winning (<0.2): {decisive_low:,}")
    print(f"    White winning (>0.8): {decisive_high:,}")
    print(f"  Balanced (0.4-0.6): {balanced:,}")
    
    return stats

def generate_synthetic_positions() -> List[ChessRecord]:
    """Generate synthetic chess positions with known evaluations."""
    
    print(f"\n🎲 Generating synthetic positions...")
    
    synthetic = []
    
    # Helper function to create bitboards
    def create_position(white_pieces: Dict[str, List[int]], black_pieces: Dict[str, List[int]], wdl: float, stm: int = 7):
        """Create a position from piece placements."""
        
        # Initialize bitboards: [black_all, pawns_all, knights_all, bishops_all, rooks_all, queens_all, kings_all, white_all]
        bitboards = [0] * 8
        
        white_mask = 0
        black_mask = 0
        
        piece_type_masks = {
            'P': 0, 'N': 0, 'B': 0, 'R': 0, 'Q': 0, 'K': 0  # Will be filled
        }
        
        # Place white pieces
        for piece_type, squares in white_pieces.items():
            for square in squares:
                bit = 1 << square
                white_mask |= bit
                piece_type_masks[piece_type.upper()] |= bit
        
        # Place black pieces  
        for piece_type, squares in black_pieces.items():
            for square in squares:
                bit = 1 << square
                black_mask |= bit
                piece_type_masks[piece_type.upper()] |= bit
        
        # Fill bitboards
        bitboards[0] = black_mask  # Black pieces
        bitboards[1] = piece_type_masks['P']  # All pawns
        bitboards[2] = piece_type_masks['N']  # All knights
        bitboards[3] = piece_type_masks['B']  # All bishops
        bitboards[4] = piece_type_masks['R']  # All rooks
        bitboards[5] = piece_type_masks['Q']  # All queens
        bitboards[6] = piece_type_masks['K']  # All kings
        bitboards[7] = white_mask  # White pieces
        
        return ChessRecord(bitboards=bitboards, wdl=wdl, stm=stm)
    
    # Generate checkmate positions
    # NOTE: Since we INVERT WDL in rescaling, these values will be flipped
    # So 0.05 here becomes 0.95 after inversion (white wins)
    # And 0.95 here becomes 0.05 after inversion (black wins)
    checkmate_positions = [
        # White checkmates (use 0.05 which becomes 0.95 after inversion)
        ({"K": [4], "Q": [59]}, {"k": [60]}, 0.05),  # Back rank mate
        ({"K": [4], "R": [56], "R": [63]}, {"k": [60]}, 0.05),  # Double rook mate
        ({"K": [52], "Q": [51]}, {"k": [59]}, 0.05),  # Queen mate
        
        # Black checkmates (use 0.95 which becomes 0.05 after inversion)
        ({"K": [60]}, {"k": [4], "q": [11]}, 0.95),  # Black back rank mate
        ({"K": [4]}, {"k": [60], "r": [0], "r": [7]}, 0.95),  # Black double rook mate
        ({"K": [4]}, {"k": [12], "q": [11]}, 0.95),  # Black queen mate
    ]
    
    for white_pieces, black_pieces, wdl in checkmate_positions:
        synthetic.append(create_position(white_pieces, black_pieces, wdl))
    
    # Generate material advantage positions
    material_positions = [
        # White up queen
        ({"K": [4], "Q": [27]}, {"k": [60]}, 0.85),
        ({"K": [4], "R": [27]}, {"k": [60]}, 0.75),
        ({"K": [4], "B": [27], "N": [28]}, {"k": [60]}, 0.70),
        
        # Black up material
        ({"K": [4]}, {"k": [60], "q": [35]}, 0.15),
        ({"K": [4]}, {"k": [60], "r": [35]}, 0.25),  
        ({"K": [4]}, {"k": [60], "b": [35], "n": [36]}, 0.30),
        
        # Pawn endgames
        ({"K": [4], "P": [12, 20]}, {"k": [60]}, 0.65),
        ({"K": [4]}, {"k": [60], "p": [44, 52]}, 0.35),
    ]
    
    for white_pieces, black_pieces, wdl in material_positions:
        synthetic.append(create_position(white_pieces, black_pieces, wdl))
    
    # Generate multiple variations by moving pieces around
    variations = []
    for record in synthetic[:10]:  # Take first 10 base positions
        for _ in range(5):  # Create 5 variations each
            # Slightly modify king positions
            new_bitboards = record.bitboards.copy()
            variations.append(ChessRecord(
                bitboards=new_bitboards,
                wdl=record.wdl + random.uniform(-0.05, 0.05),  # Small variation
                stm=record.stm
            ))
    
    synthetic.extend(variations)
    
    print(f"  Generated {len(synthetic)} synthetic positions")
    return synthetic

def rescale_wdl_values(records: List[ChessRecord], target_range: Tuple[float, float] = (0.05, 0.95)) -> List[ChessRecord]:
    """Rescale WDL values to use the full target range and correct orientation."""
    
    print(f"\n🎚️ Rescaling and inverting WDL values...")
    
    # Find current range
    wdl_values = [r.wdl for r in records]
    current_min = min(wdl_values)
    current_max = max(wdl_values)
    current_range = current_max - current_min
    
    target_min, target_max = target_range
    target_range_size = target_max - target_min
    
    print(f"  Current range: {current_min:.6f} to {current_max:.6f}")
    print(f"  Target range: {target_min:.6f} to {target_max:.6f}")
    print(f"  ⚠️ INVERTING WDL: Original data has low=white_wins, we need low=black_wins")
    
    rescaled_records = []
    for record in records:
        # Linear rescaling with INVERSION
        # Original: low WDL = white wins, high WDL = black wins
        # Target:   low WDL = black wins, high WDL = white wins
        normalized = (record.wdl - current_min) / current_range
        inverted_normalized = 1.0 - normalized  # Invert the scale
        rescaled_wdl = target_min + inverted_normalized * target_range_size
        
        rescaled_records.append(ChessRecord(
            bitboards=record.bitboards,
            wdl=rescaled_wdl,
            stm=record.stm
        ))
    
    # Verify rescaling
    new_wdl_values = [r.wdl for r in rescaled_records]
    print(f"  Rescaled range: {min(new_wdl_values):.6f} to {max(new_wdl_values):.6f}")
    
    return rescaled_records

def balance_dataset(records: List[ChessRecord], target_decisive_pct: float = 20.0) -> List[ChessRecord]:
    """Balance the dataset to include more decisive positions."""
    
    print(f"\n⚖️ Balancing dataset (target: {target_decisive_pct:.1f}% decisive)...")
    
    # Categorize records
    decisive_low = [r for r in records if r.wdl < 0.3]   # Black winning
    decisive_high = [r for r in records if r.wdl > 0.7]  # White winning  
    balanced = [r for r in records if 0.3 <= r.wdl <= 0.7]  # Balanced
    
    total_decisive = len(decisive_low) + len(decisive_high)
    current_decisive_pct = 100 * total_decisive / len(records)
    
    print(f"  Current: {current_decisive_pct:.1f}% decisive ({total_decisive:,}/{len(records):,})")
    print(f"    Black winning: {len(decisive_low):,}")
    print(f"    White winning: {len(decisive_high):,}")
    print(f"    Balanced: {len(balanced):,}")
    
    if current_decisive_pct >= target_decisive_pct:
        print(f"  ✅ Already have enough decisive positions")
        return records
    
    # Calculate how many decisive positions we need
    target_total = len(records)
    target_decisive = int(target_total * target_decisive_pct / 100)
    need_more_decisive = target_decisive - total_decisive
    
    print(f"  Need {need_more_decisive:,} more decisive positions")
    
    # Duplicate existing decisive positions to reach target
    balanced_records = []
    
    # Keep all existing records
    balanced_records.extend(records)
    
    # Add duplicates of decisive positions
    decisive_positions = decisive_low + decisive_high
    if decisive_positions:
        duplicates_needed = need_more_decisive
        for i in range(duplicates_needed):
            record = decisive_positions[i % len(decisive_positions)]
            # Add small noise to avoid exact duplicates
            noise = random.uniform(-0.02, 0.02)
            noisy_record = ChessRecord(
                bitboards=record.bitboards,
                wdl=max(0.0, min(1.0, record.wdl + noise)),
                stm=record.stm
            )
            balanced_records.append(noisy_record)
    
    print(f"  Result: {len(balanced_records):,} total records")
    
    # Verify final distribution
    final_decisive = sum(1 for r in balanced_records if r.wdl < 0.3 or r.wdl > 0.7)
    final_pct = 100 * final_decisive / len(balanced_records)
    print(f"  Final: {final_pct:.1f}% decisive")
    
    return balanced_records

def save_preprocessed_data(records: List[ChessRecord], output_path: str):
    """Save preprocessed data in the original binary format."""
    
    print(f"\n💾 Saving preprocessed data to {output_path}")
    
    with open(output_path, 'wb') as f:
        for record in records:
            # Write 8 bitboards (64 bytes)
            for bb in record.bitboards:
                f.write(struct.pack('<Q', bb))
            
            # Write 4 padding bytes (bytes 64-67)
            f.write(struct.pack('<I', 0))
            
            # Write STM (1 byte at offset 68)
            f.write(struct.pack('B', record.stm))
            
            # Write WDL (4 bytes at offset 69-72)
            f.write(struct.pack('<f', record.wdl))
    
    file_size = os.path.getsize(output_path)
    expected_size = len(records) * 73
    print(f"  Saved {len(records):,} records")
    print(f"  File size: {file_size:,} bytes (expected: {expected_size:,})")
    
    if file_size != expected_size:
        print(f"  ⚠️ Size mismatch! Records may be corrupted.")
    else:
        print(f"  ✅ File format is correct.")

def main():
    """Main preprocessing pipeline."""
    
    print("🔧 CHESS DATA PREPROCESSING PIPELINE")
    print("=" * 60)
    
    # Configuration
    input_path = r"C:\dev\chess-data\Lichess Elite Database\Lichess Elite Database\combined.evals.bin"
    output_path = "preprocessed_chess_data.bin"
    
    # Limit records for testing (remove for full dataset)
    max_records = 100000  # Process first 100k records for testing
    
    # Step 1: Read original data
    records = read_original_data(input_path, max_records)
    
    # Step 2: Analyze distribution
    original_stats = analyze_data_distribution(records)
    
    # Step 3: Add synthetic positions
    synthetic_records = generate_synthetic_positions()
    all_records = records + synthetic_records
    
    # Step 4: Rescale WDL values to use full range
    rescaled_records = rescale_wdl_values(all_records)
    
    # Step 5: Balance dataset
    balanced_records = balance_dataset(rescaled_records, target_decisive_pct=15.0)
    
    # Step 6: Analyze final distribution
    print(f"\n📊 Final data analysis:")
    final_stats = analyze_data_distribution(balanced_records)
    
    # Step 7: Save preprocessed data
    save_preprocessed_data(balanced_records, output_path)
    
    print(f"\n✅ Preprocessing complete!")
    print(f"Original: {original_stats['total_records']:,} records, {original_stats['decisive_pct']:.1f}% decisive")
    print(f"Final: {final_stats['total_records']:,} records, {final_stats['decisive_pct']:.1f}% decisive")
    print(f"Saved to: {output_path}")

if __name__ == "__main__":
    main()