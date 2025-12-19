import os
import sys
from contextlib import closing
from collections import defaultdict

import numpy as np

from data import record_dtype, RECORD_SIZE


SYZYGY_DEFAULT_PATH = "C:\\dev\\chess-data\\syzygy"

# File mirroring lookup table: maps each square to its horizontally mirrored square
# a1(0)->h1(7), b1(1)->g1(6), etc.
MIRROR_SQUARES = np.array([
    7,  6,  5,  4,  3,  2,  1,  0,
    15, 14, 13, 12, 11, 10,  9,  8,
    23, 22, 21, 20, 19, 18, 17, 16,
    31, 30, 29, 28, 27, 26, 25, 24,
    39, 38, 37, 36, 35, 34, 33, 32,
    47, 46, 45, 44, 43, 42, 41, 40,
    55, 54, 53, 52, 51, 50, 49, 48,
    63, 62, 61, 60, 59, 58, 57, 56,
], dtype=np.uint8)

def mirror_bitboard(bb: np.uint64) -> np.uint64:
    """Mirror a bitboard horizontally (flip files a<->h)."""
    # Swap adjacent files using bit manipulation
    # This is the standard horizontal flip for chess bitboards
    k1 = np.uint64(0x5555555555555555)  # odd bits
    k2 = np.uint64(0x3333333333333333)  # pairs
    k4 = np.uint64(0x0f0f0f0f0f0f0f0f)  # nibbles
    bb = ((bb >> 1) & k1) | ((bb & k1) << 1)  # swap adjacent bits
    bb = ((bb >> 2) & k2) | ((bb & k2) << 2)  # swap adjacent pairs
    bb = ((bb >> 4) & k4) | ((bb & k4) << 4)  # swap adjacent nibbles
    return bb

def mirror_position(pos):
    """Create a horizontally mirrored copy of a position record."""
    mirrored = pos.copy()
    
    # Mirror all bitboards
    mirrored["bb_black"] = mirror_bitboard(pos["bb_black"])
    mirrored["bb_white"] = mirror_bitboard(pos["bb_white"])
    mirrored["bb_pawns"] = mirror_bitboard(pos["bb_pawns"])
    mirrored["bb_knights"] = mirror_bitboard(pos["bb_knights"])
    mirrored["bb_bishops"] = mirror_bitboard(pos["bb_bishops"])
    mirrored["bb_rooks"] = mirror_bitboard(pos["bb_rooks"])
    mirrored["bb_queens"] = mirror_bitboard(pos["bb_queens"])
    mirrored["bb_kings"] = mirror_bitboard(pos["bb_kings"])
    
    # Mirror castling rights: swap kingside/queenside for each color.
    # Engine format (see CastlingRights in Lolbot.Engine):
    #   WhiteQueen=1, WhiteKing=2, BlackQueen=4, BlackKing=8
    old_castling = int(pos["castling"])
    new_castling = 0
    if old_castling & 2: new_castling |= 1  # WK -> WQ
    if old_castling & 1: new_castling |= 2  # WQ -> WK
    if old_castling & 8: new_castling |= 4  # BK -> BQ
    if old_castling & 4: new_castling |= 8  # BQ -> BK
    mirrored["castling"] = np.uint8(new_castling)
    
    # Mirror en passant square.
    # IMPORTANT: the binary format stores EnPassant as a square index (LERF, a1=0..h8=63),
    # with 0 meaning "no en passant" (and EP can never legally be a1 anyway).
    ep_sq = int(pos["ep_file"])
    if ep_sq != 0:
        mirrored["ep_file"] = np.uint8(ep_sq ^ 7)  # flip file within the same rank
    
    # stm, eval, wdl stay the same
    return mirrored

def truncate_to_full_records(file_path):
    """Ensure the file length is an integer multiple of RECORD_SIZE."""
    try:
        size = os.path.getsize(file_path)
    except OSError:
        return False

    remainder = size % RECORD_SIZE
    if remainder == 0:
        return True

    new_size = size - remainder
    if new_size <= 0:
        print(f"  Skipping {file_path}: file smaller than one record ({size} bytes)")
        return False

    try:
        with open(file_path, "rb+") as fh:
            fh.truncate(new_size)
        print(f"  Truncated {file_path} from {size} to {new_size} bytes")
        return True
    except OSError as exc:
        print(f"  Failed to truncate {file_path}: {exc}")
        return False

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


def record_to_board(rec, chess_module):
    """Reconstruct a python-chess Board from a record."""
    board = chess_module.Board(None)

    white_mask = int(rec["bb_white"])
    black_mask = int(rec["bb_black"])

    piece_masks = (
        (chess_module.PAWN, int(rec["bb_pawns"])),
        (chess_module.KNIGHT, int(rec["bb_knights"])),
        (chess_module.BISHOP, int(rec["bb_bishops"])),
        (chess_module.ROOK, int(rec["bb_rooks"])),
        (chess_module.QUEEN, int(rec["bb_queens"])),
        (chess_module.KING, int(rec["bb_kings"])),
    )

    for color, color_mask in ((chess_module.WHITE, white_mask), (chess_module.BLACK, black_mask)):
        for piece_type, mask in piece_masks:
            color_piece_mask = mask & color_mask
            while color_piece_mask:
                lsb = color_piece_mask & -color_piece_mask
                square_index = lsb.bit_length() - 1
                board.set_piece_at(square_index, chess_module.Piece(piece_type, color))
                color_piece_mask ^= lsb

    board.turn = bool(int(rec["stm"]))
    board.castling_rights = chess_module.BB_EMPTY
    board.ep_square = None
    board.halfmove_clock = 0
    board.fullmove_number = 1

    return board


def rescore_with_syzygy(all_positions, piece_counts, syzygy_path):
    """Apply Syzygy tablebases to rescore eligible endgames."""
    try:
        import chess
        import chess.syzygy
    except ImportError:
        print("Syzygy rescoring skipped: python-chess is not installed")
        return 0, {}

    wdl_dir = os.path.join(syzygy_path, "3-4-5-wdl")
    if not os.path.isdir(wdl_dir):
        print(f"Syzygy rescoring skipped: directory not found ({wdl_dir})")
        return 0, {}

    eligible_indices = np.flatnonzero((piece_counts >= 3) & (piece_counts <= 5))
    if eligible_indices.size == 0:
        return 0, {}

    distribution = {"win": 0, "draw": 0, "loss": 0}
    rescored = 0

    try:
        tablebase = chess.syzygy.Tablebase()
        tablebase.add_directory(wdl_dir, load_wdl=True, load_dtz=False)

        for idx in eligible_indices:
            rec = all_positions[idx]
            try:
                board = record_to_board(rec, chess)
            except ValueError:
                print(f"  Warning: invalid board position at index {idx}, skipping Syzygy probe")
                continue

            try:
                wdl = tablebase.probe_wdl(board)
            except chess.syzygy.MissingTableError as err:
                continue

            rescored += 1
            if wdl > 0:
                all_positions[idx]["wdl_f32"] = np.float32(1.0)
                all_positions[idx]["eval_i16"] = np.int16(200)
                distribution["win"] += 1
            elif wdl < 0:
                all_positions[idx]["wdl_f32"] = np.float32(0.0)
                all_positions[idx]["eval_i16"] = np.int16(-200)
                distribution["loss"] += 1
            else:
                all_positions[idx]["wdl_f32"] = np.float32(0.5)
                all_positions[idx]["eval_i16"] = np.int16(0)
                distribution["draw"] += 1
    except OSError as exc:
        print(f"Syzygy rescoring skipped: failed to open tablebase ({exc})")
        return 0, {}

    return rescored, distribution

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
        truncate_to_full_records(file_path)
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

    occupancy = np.bitwise_or(all_positions["bb_white"], all_positions["bb_black"])
    piece_counts = np.fromiter((int(int(val).bit_count()) for val in occupancy), dtype=np.uint8, count=len(all_positions))

    # Filter out mate-score positions unless we are in sparse endgames (<6 pieces)
    print("Filtering mate-score positions...")
    evals = all_positions["eval_i16"].astype(np.int32)
    mate_mask = np.abs(evals) >= 10000
    mate_indices = np.flatnonzero(mate_mask)
    if mate_indices.size:
        filter_mask = np.ones(len(all_positions), dtype=bool)
        filter_mask[mate_indices] = piece_counts[mate_indices] < 6
        removed = len(all_positions) - int(filter_mask.sum())
        if removed > 0:
            all_positions = all_positions[filter_mask]
            occupancy = occupancy[filter_mask]
            piece_counts = piece_counts[filter_mask]
        print(f"  Removed {removed:,} mate-score positions with >=6 pieces")
    else:
        print("  No mate-score positions filtered")

    print("Applying Syzygy tablebases to eligible endgames...")
    rescored, distribution = rescore_with_syzygy(all_positions, piece_counts, SYZYGY_DEFAULT_PATH)
    if rescored:
        print(f"  Rescored {rescored:,} positions (wins: {distribution.get('win', 0):,}, draws: {distribution.get('draw', 0):,}, losses: {distribution.get('loss', 0):,})")
    else:
        print("  No positions rescored via Syzygy")
    
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
    
    # Create mirrored versions of all positions
    print("Creating horizontally mirrored positions...")
    mirrored_positions = np.empty(len(final_positions), dtype=record_dtype)
    for i in range(len(final_positions)):
        if i % 1000000 == 0 and i > 0:
            print(f"  Mirrored {i:,}/{len(final_positions):,} positions ({100*i/len(final_positions):.1f}%)")
        mirrored_positions[i] = mirror_position(final_positions[i])
    print(f"  Created {len(mirrored_positions):,} mirrored positions")
    
    # Combine original and mirrored
    final_positions = np.concatenate([final_positions, mirrored_positions])
    print(f"  Total positions after mirroring: {len(final_positions):,}")
    
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
    
    # -------------------------
    # Dataset Statistics Summary
    # -------------------------
    print("\n" + "=" * 60)
    print("DATASET STATISTICS SUMMARY")
    print("=" * 60)
    
    # Evaluation distribution
    evals = final_positions["eval_i16"].astype(np.int32)
    print("\n📊 Evaluation Distribution:")
    print(f"  Min eval:    {evals.min():+,} cp")
    print(f"  Max eval:    {evals.max():+,} cp")
    print(f"  Mean eval:   {evals.mean():+.1f} cp")
    print(f"  Median eval: {np.median(evals):+.1f} cp")
    print(f"  Std dev:     {evals.std():.1f} cp")
    
    # Eval buckets
    print("\n  Eval buckets:")
    buckets = [
        ("  |eval| ≤ 50 cp (equal)", np.sum(np.abs(evals) <= 50)),
        ("  |eval| ≤ 100 cp", np.sum(np.abs(evals) <= 100)),
        ("  |eval| ≤ 200 cp", np.sum(np.abs(evals) <= 200)),
        ("  |eval| ≤ 500 cp", np.sum(np.abs(evals) <= 500)),
        ("  |eval| > 500 cp (decisive)", np.sum(np.abs(evals) > 500)),
    ]
    for label, count in buckets:
        pct = 100 * count / len(evals)
        print(f"    {label}: {count:,} ({pct:.1f}%)")
    
    # WDL distribution
    wdls = final_positions["wdl_f32"]
    print("\n📊 WDL (Game Result) Distribution:")
    white_wins = np.sum(wdls > 0.75)
    draws = np.sum((wdls >= 0.25) & (wdls <= 0.75))
    black_wins = np.sum(wdls < 0.25)
    print(f"  White wins (WDL > 0.75): {white_wins:,} ({100*white_wins/len(wdls):.1f}%)")
    print(f"  Draws (0.25 ≤ WDL ≤ 0.75): {draws:,} ({100*draws/len(wdls):.1f}%)")
    print(f"  Black wins (WDL < 0.25): {black_wins:,} ({100*black_wins/len(wdls):.1f}%)")
    print(f"  Mean WDL:   {wdls.mean():.3f}")
    print(f"  Median WDL: {np.median(wdls):.3f}")
    
    # Side to move distribution
    stm = final_positions["stm"]
    white_to_move = np.sum(stm == 7)
    black_to_move = np.sum(stm == 0)
    print("\n📊 Side to Move:")
    print(f"  White to move: {white_to_move:,} ({100*white_to_move/len(stm):.1f}%)")
    print(f"  Black to move: {black_to_move:,} ({100*black_to_move/len(stm):.1f}%)")
    
    # Piece count distribution (game phase proxy)
    occupancy = np.bitwise_or(final_positions["bb_white"], final_positions["bb_black"])
    piece_counts = np.array([int(int(val).bit_count()) for val in occupancy], dtype=np.uint8)
    print("\n📊 Piece Count Distribution (Game Phase):")
    print(f"  Min pieces:  {piece_counts.min()}")
    print(f"  Max pieces:  {piece_counts.max()}")
    print(f"  Mean pieces: {piece_counts.mean():.1f}")
    
    phase_buckets = [
        ("  Endgame (2-6 pieces)", (piece_counts >= 2) & (piece_counts <= 6)),
        ("  Late middlegame (7-12 pieces)", (piece_counts >= 7) & (piece_counts <= 12)),
        ("  Middlegame (13-20 pieces)", (piece_counts >= 13) & (piece_counts <= 20)),
        ("  Opening (21-32 pieces)", (piece_counts >= 21) & (piece_counts <= 32)),
    ]
    for label, mask in phase_buckets:
        count = np.sum(mask)
        pct = 100 * count / len(piece_counts)
        print(f"    {label}: {count:,} ({pct:.1f}%)")
    
    # Eval vs WDL agreement
    print("\n📊 Eval-WDL Agreement:")
    eval_says_white = evals > 50
    eval_says_black = evals < -50
    wdl_says_white = wdls > 0.6
    wdl_says_black = wdls < 0.4
    
    agree_white = np.sum(eval_says_white & wdl_says_white)
    agree_black = np.sum(eval_says_black & wdl_says_black)
    disagree = np.sum((eval_says_white & wdl_says_black) | (eval_says_black & wdl_says_white))
    
    print(f"  Eval & WDL agree (white winning): {agree_white:,}")
    print(f"  Eval & WDL agree (black winning): {agree_black:,}")
    print(f"  Eval & WDL disagree: {disagree:,} ({100*disagree/len(evals):.2f}%)")
    
    print("\n" + "=" * 60)
    print("PREPROCESSING COMPLETE")
    print("=" * 60)
    
    return final_positions

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python 0_pre_process.py <folder_path>")
        sys.exit(1)
    process_folder(sys.argv[1])