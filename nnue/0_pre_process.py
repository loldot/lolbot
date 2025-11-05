import os
import sys
import numpy as np
from collections import defaultdict
from data import record_dtype, RECORD_SIZE

def read_positions_from_bin(file_path):
    """Read chess positions from binary file using numpy memmap."""
    try:
        # Memory-map the file for efficient reading
        positions = np.memmap(file_path, dtype=record_dtype, mode='r')
        return positions
    except Exception:
        # Silently ignore files that can't be read
        return np.array([], dtype=record_dtype)

def categorize_position(pos):
    """Categorize a position based on evaluation and WDL."""
    # Convert i16 evaluation to centipawns (assuming it's already in cp)
    cp = int(pos["eval_i16"])
    wdl = float(pos["wdl_f32"])
    almost_equal = abs(cp) <= 100
    wdl_low = wdl < 0.5
    return (almost_equal, wdl_low)

def process_folder(folder_path):
    # Find all files to process
    bin_files = [f for f in os.listdir(folder_path) if f.endswith('.pgn.evals.bin')]
    print(f"Found {len(bin_files)} .pgn.evals.bin files to process")
    
    all_positions = []
    successful_files = 0
    failed_files = 0
    
    for i, fname in enumerate(bin_files, 1):
        print(f"Processing file {i}/{len(bin_files)}: {fname}", end=" ... ")
        file_path = os.path.join(folder_path, fname)
        positions = read_positions_from_bin(file_path)
        
        if len(positions) > 0:
            all_positions.append(positions)
            successful_files += 1
            print(f"OK ({len(positions):,} positions)")
        else:
            failed_files += 1
            print("FAILED (skipping)")

    print(f"\nProcessing complete: {successful_files} successful, {failed_files} failed")
    
    if not all_positions:
        print("No valid position files found.")
        return

    # Concatenate all position arrays
    print("\nConcatenating position arrays...")
    all_positions = np.concatenate(all_positions)
    print(f"Total positions loaded: {len(all_positions):,}")
    
    # Remove duplicates by converting to structured array and using numpy unique
    print("Removing duplicates (this may take a while for large datasets)...")
    unique_positions, unique_indices = np.unique(all_positions, return_index=True)
    duplicates_removed = len(all_positions) - len(unique_positions)
    print(f"Duplicates removed: {duplicates_removed:,}")
    print(f"Unique positions: {len(unique_positions):,}")

    # Categorize positions
    print("Categorizing positions...")
    categories = defaultdict(list)
    for i, pos in enumerate(unique_positions):
        if i % 100000 == 0 and i > 0:
            print(f"  Categorized {i:,}/{len(unique_positions):,} positions ({i/len(unique_positions)*100:.1f}%)")
        cat = categorize_position(pos)
        categories[cat].append(pos)
    print(f"  Categorization complete: {len(unique_positions):,} positions processed")

    # Print category distribution
    print("Category distribution:")
    category_counts = {}
    for cat, positions in categories.items():
        almost_equal, wdl_low = cat
        count = len(positions)
        category_counts[cat] = count
        print(f"  Almost equal: {almost_equal}, WDL low: {wdl_low} -> {count} positions")

    # Collect all positions with their category counts
    all_processed_positions = []
    running_counts = {cat: 0 for cat in [(True, True), (True, False), (False, True), (False, False)]}
    
    for cat in [(True, True), (True, False), (False, True), (False, False)]:
        positions = categories.get(cat, [])
        running_counts[cat] = len(positions)
        all_processed_positions.extend(positions)
        
    print(f"Running category counts: {running_counts}")

    print(f"\nTotal processed positions count: {len(all_processed_positions):,}")
    
    # Convert back to numpy array for saving
    print("Converting to numpy array for saving...")
    final_positions = np.array(all_processed_positions, dtype=record_dtype)
    
    # Save processed positions to a new file
    output_file = os.path.join(folder_path, "preprocessed_positions.bin")
    print(f"Saving to: {output_file}")
    final_positions.tofile(output_file)
    
    # Verify the saved file
    file_size = os.path.getsize(output_file)
    expected_size = len(final_positions) * RECORD_SIZE
    print(f"✓ Saved {len(final_positions):,} positions")
    print(f"✓ File size: {file_size:,} bytes ({file_size // RECORD_SIZE:,} records × {RECORD_SIZE} bytes)")
    
    if file_size == expected_size:
        print("✓ File format validation passed")
    else:
        print(f"⚠️  Warning: File size mismatch! Expected {expected_size:,}, got {file_size:,}")
    
    return final_positions

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python 0_pre_process.py <folder_path>")
        sys.exit(1)
    process_folder(sys.argv[1])