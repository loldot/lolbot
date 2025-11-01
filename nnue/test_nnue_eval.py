"""
Test script to evaluate positions using the trained NNUE model
and convert FEN positions to evaluation scores.
"""

import torch
import numpy as np
import struct

# Load the NNUE model architecture
from train_nnue_proper import ProperNNUE, ProperChessDataset

def load_nnue_model(weights_path="nnue_weights_best.pth", hidden_size=16):
    """Load the trained NNUE model"""
    model = ProperNNUE(input_size=768, hidden_size=hidden_size)
    model.load_state_dict(torch.load(weights_path, map_location='cpu'))
    model.eval()
    return model

def fen_to_bitboards(fen):
    """
    Convert a FEN string to the bitboard format used by our dataset.
    Returns a record-like structure with bitboards.
    """
    # This is a simplified conversion - you'd need a full chess library for complete FEN parsing
    # For now, let's assume we have the starting position
    parts = fen.split()
    board_str = parts[0]
    stm = 0 if parts[1] == 'b' else 7  # 0 for black, 7 for white
    
    # Initialize empty bitboards
    bitboards = {
        'bb_black': 0,
        'bb_white': 0, 
        'bb_pawns': 0,
        'bb_knights': 0,
        'bb_bishops': 0,
        'bb_rooks': 0,
        'bb_queens': 0,
        'bb_kings': 0,
        'stm': stm
    }
    
    # Parse the board (this is simplified - need proper FEN parsing)
    rank = 7
    file = 0
    
    for char in board_str:
        if char == '/':
            rank -= 1
            file = 0
        elif char.isdigit():
            file += int(char)
        else:
            square = rank * 8 + file
            bit = 1 << square
            
            # Determine piece type and color
            piece_type = None
            is_white = char.isupper()
            char_lower = char.lower()
            
            if char_lower == 'p':
                piece_type = 'bb_pawns'
            elif char_lower == 'n':
                piece_type = 'bb_knights'  
            elif char_lower == 'b':
                piece_type = 'bb_bishops'
            elif char_lower == 'r':
                piece_type = 'bb_rooks'
            elif char_lower == 'q':
                piece_type = 'bb_queens'
            elif char_lower == 'k':
                piece_type = 'bb_kings'
            
            if piece_type:
                bitboards[piece_type] |= bit
                if is_white:
                    bitboards['bb_white'] |= bit
                else:
                    bitboards['bb_black'] |= bit
            
            file += 1
    
    return bitboards

def evaluate_fen(model, fen):
    """Evaluate a FEN position using the trained NNUE model"""
    # Convert FEN to bitboards
    bitboards = fen_to_bitboards(fen)
    
    # Create a fake record structure
    from train_nnue_proper import _planes_from_record_c_style_with_stm
    
    # Convert to numpy arrays matching our record format
    rec = np.array((
        bitboards['bb_black'], bitboards['bb_pawns'], bitboards['bb_knights'], 
        bitboards['bb_bishops'], bitboards['bb_rooks'], bitboards['bb_queens'],
        bitboards['bb_kings'], bitboards['bb_white'], bitboards['stm'], 0, 0, 0, 0.0
    ), dtype=[
        ('bb_black', '<u8'), ('bb_pawns', '<u8'), ('bb_knights', '<u8'),
        ('bb_bishops', '<u8'), ('bb_rooks', '<u8'), ('bb_queens', '<u8'), 
        ('bb_kings', '<u8'), ('bb_white', '<u8'), ('stm', 'u1'),
        ('castling', 'u1'), ('ep_file', 'u1'), ('eval_i16', '<i2'), ('wdl_f32', '<f4')
    ])
    
    # Extract features
    features = _planes_from_record_c_style_with_stm(rec)
    
    # Convert to tensor and evaluate
    x = torch.from_numpy(features).unsqueeze(0)  # Add batch dimension
    
    with torch.no_grad():
        # Get WDL probability
        wdl_prob = model(x).item()
        
        # Convert to centipawns (inverse sigmoid with k=400)
        if wdl_prob <= 0.001:
            centipawns = -4000  # Very bad for white
        elif wdl_prob >= 0.999:
            centipawns = 4000   # Very good for white
        else:
            centipawns = 400 * np.log(wdl_prob / (1 - wdl_prob))
    
    return wdl_prob, centipawns

def test_positions():
    """Test the model on some known positions"""
    print("Loading NNUE model...")
    model = load_nnue_model()
    
    # Test positions
    positions = [
        ("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", "Starting position"),
        ("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", "1.e4"),
        ("r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3", "1.e4 e5 2.Nf3 Nc6"),
        ("8/8/8/8/8/8/8/K6k w - - 0 1", "Kings only"),
        ("rnbqkb1r/pppp1ppp/5n2/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 4 3", "Italian opening setup"),
    ]
    
    print("\nEvaluating positions:")
    print("-" * 70)
    
    for fen, description in positions:
        try:
            wdl_prob, centipawns = evaluate_fen(model, fen)
            print(f"{description:25s} WDL: {wdl_prob:.3f}  CP: {centipawns:+7.0f}")
        except Exception as e:
            print(f"{description:25s} ERROR: {e}")

def inspect_weights():
    """Inspect the saved binary weights file"""
    print("\nInspecting nnue_weights.bin...")
    
    with open("nnue_weights.bin", "rb") as f:
        # Read dimensions based on our known architecture
        hidden_size = 16
        input_size = 768
        
        # Hidden weights: [16, 768]  
        hidden_weights = []
        for i in range(hidden_size * input_size):
            weight = struct.unpack('f', f.read(4))[0]
            hidden_weights.append(weight)
        
        # Hidden bias: [16]
        hidden_bias = []
        for i in range(hidden_size):
            bias = struct.unpack('f', f.read(4))[0]
            hidden_bias.append(bias)
        
        # Output weights: [16]
        output_weights = []
        for i in range(hidden_size):
            weight = struct.unpack('f', f.read(4))[0]
            output_weights.append(weight)
        
        # Output bias: [1]
        output_bias = struct.unpack('f', f.read(4))[0]
    
    print(f"Hidden weights: {len(hidden_weights)} values, range: [{min(hidden_weights):.4f}, {max(hidden_weights):.4f}]")
    print(f"Hidden bias: {len(hidden_bias)} values, range: [{min(hidden_bias):.4f}, {max(hidden_bias):.4f}]")  
    print(f"Output weights: {len(output_weights)} values, range: [{min(output_weights):.4f}, {max(output_weights):.4f}]")
    print(f"Output bias: {output_bias:.4f}")
    
    # Calculate expected file size
    expected_size = (hidden_size * input_size + hidden_size + hidden_size + 1) * 4  # 4 bytes per float
    import os
    actual_size = os.path.getsize("nnue_weights.bin")
    print(f"File size: {actual_size} bytes (expected: {expected_size} bytes)")
    
    if actual_size == expected_size:
        print("✓ Binary format looks correct!")
    else:
        print("✗ Binary format size mismatch!")

def main():
    inspect_weights()
    test_positions()

if __name__ == "__main__":
    main()