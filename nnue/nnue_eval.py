#!/usr/bin/env python3
"""
NNUE Inference Script
Reads FEN strings from stdin and outputs neural network evaluations.
"""

import sys
import torch
import numpy as np
from typing import Tuple

# Import the NNUE model class
from data import _u64_to_bits_le

class NNUE(torch.nn.Module):
    def __init__(self, input_size: int, hidden_size: int = 32):
        super().__init__()
        self.input_size = input_size
        self.hidden = torch.nn.Linear(input_size, hidden_size)
        self.output = torch.nn.Linear(hidden_size, 1)

    def forward(self, x):
        # Clipped ReLU (CReLU) like activation: clamp between 0 and 1
        x = torch.clamp(self.hidden(x), 0, 1)
        return torch.sigmoid(self.output(x))

def fen_to_bitboards(fen: str) -> Tuple[np.ndarray, bool]:
    """
    Convert FEN string to bitboards matching training data format.
    Returns (features_768, is_black_to_move)
    """
    parts = fen.strip().split()
    if len(parts) < 2:
        raise ValueError("Invalid FEN: missing parts")
    
    board_str = parts[0]
    active_color = parts[1]
    
    # Initialize bitboards
    bb_white = np.uint64(0)
    bb_black = np.uint64(0)
    bb_pawns = np.uint64(0)
    bb_knights = np.uint64(0)
    bb_bishops = np.uint64(0)
    bb_rooks = np.uint64(0)
    bb_queens = np.uint64(0)
    bb_kings = np.uint64(0)
    
    # Parse board position
    rank = 7  # Start from rank 8 (index 7)
    file = 0  # Start from file a (index 0)
    
    for char in board_str:
        if char == '/':
            rank -= 1
            file = 0
        elif char.isdigit():
            file += int(char)  # Skip empty squares
        else:
            # Calculate square index (a1=0, h8=63)
            square = rank * 8 + file
            bit_mask = np.uint64(1) << square
            
            # Set color bitboards
            if char.isupper():  # White piece
                bb_white |= bit_mask
            else:  # Black piece
                bb_black |= bit_mask
            
            # Set piece type bitboards
            piece_type = char.lower()
            if piece_type == 'p':
                bb_pawns |= bit_mask
            elif piece_type == 'n':
                bb_knights |= bit_mask
            elif piece_type == 'b':
                bb_bishops |= bit_mask
            elif piece_type == 'r':
                bb_rooks |= bit_mask
            elif piece_type == 'q':
                bb_queens |= bit_mask
            elif piece_type == 'k':
                bb_kings |= bit_mask
            
            file += 1
    
    is_black_to_move = (active_color == 'b')
    
    # Apply the same perspective flipping as in training data
    if is_black_to_move:
        # Flip all bitboards vertically
        bb_white = _flip_bitboard_vertically(bb_white)
        bb_black = _flip_bitboard_vertically(bb_black)
        bb_pawns = _flip_bitboard_vertically(bb_pawns)
        bb_knights = _flip_bitboard_vertically(bb_knights)
        bb_bishops = _flip_bitboard_vertically(bb_bishops)
        bb_rooks = _flip_bitboard_vertically(bb_rooks)
        bb_queens = _flip_bitboard_vertically(bb_queens)
        bb_kings = _flip_bitboard_vertically(bb_kings)
        
        # Swap colors (black becomes "us", white becomes "them")
        bb_white, bb_black = bb_black, bb_white
    
    # Create feature planes in same order as training: 
    # White pawns, Black pawns, White knights, Black knights, etc.
    piece_combinations = [
        (bb_pawns & bb_white),   # White pawns
        (bb_pawns & bb_black),   # Black pawns
        (bb_knights & bb_white), # White knights
        (bb_knights & bb_black), # Black knights
        (bb_bishops & bb_white), # White bishops
        (bb_bishops & bb_black), # Black bishops
        (bb_rooks & bb_white),   # White rooks
        (bb_rooks & bb_black),   # Black rooks
        (bb_queens & bb_white),  # White queens
        (bb_queens & bb_black),  # Black queens
        (bb_kings & bb_white),   # White king
        (bb_kings & bb_black),   # Black king
    ]
    
    # Convert to 768-dimensional feature vector
    features = np.zeros(768, dtype=np.float32)
    for i, bitboard in enumerate(piece_combinations):
        bits = _u64_to_bits_le(bitboard)
        start_idx = i * 64
        features[start_idx:start_idx + 64] = bits.astype(np.float32)
    
    return features, is_black_to_move

def _flip_bitboard_vertically(bb: np.uint64) -> np.uint64:
    """
    Flip a bitboard vertically (rank 1 <-> rank 8, etc.)
    This is used to convert black-to-move positions to white perspective.
    """
    # Convert to bits, reshape to 8x8, flip vertically, then back to uint64
    bits = _u64_to_bits_le(bb)
    board = bits.reshape(8, 8)
    flipped_board = np.flip(board, axis=0)  # Flip rows
    flipped_bits = flipped_board.flatten()
    
    # Pack back to uint64
    bytes_array = np.packbits(flipped_bits.reshape(8, 8), bitorder='little', axis=1).flatten()
    return np.uint64(int.from_bytes(bytes_array.tobytes(), 'little'))

def load_model(model_path: str, input_size: int = 768, hidden_size: int = 16) -> NNUE:
    """Load trained NNUE model from file"""
    model = NNUE(input_size=input_size, hidden_size=hidden_size)
    
    try:
        state_dict = torch.load(model_path, map_location='cpu')
        model.load_state_dict(state_dict)
        model.eval()
        print(f"Loaded model from {model_path}", file=sys.stderr)
        return model
    except FileNotFoundError:
        print(f"Error: Model file not found: {model_path}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Error loading model: {e}", file=sys.stderr)
        sys.exit(1)

def evaluate_fen(model: NNUE, fen: str) -> float | None:
    """Evaluate a FEN position using the NNUE model"""
    try:
        features, is_black_to_move = fen_to_bitboards(fen)
        
        # Convert to tensor
        features_tensor = torch.from_numpy(features).unsqueeze(0)  # Add batch dimension
        
        # Get prediction
        with torch.no_grad():
            output = model(features_tensor)
            wdl_value = output.item()
        
        # Return WDL probability (0.0 = white wins, 1.0 = black wins)
        return wdl_value
    except Exception as e:
        print(f"Error evaluating FEN '{fen}': {e}", file=sys.stderr)
        return None

def main():
    """Main inference loop"""
    # Model configuration (should match training configuration)
    model_path = "nnue_weights_trained.pth"
    input_size = 768
    hidden_size = 16  # Should match training configuration
    
    # Check if model file exists
    if not os.path.exists(model_path):
        print(f"Error: Model file not found: {model_path}", file=sys.stderr)
        print("Please train a model first using 1_train.py", file=sys.stderr)
        sys.exit(1)
    
    # Load model
    model = load_model(model_path, input_size, hidden_size)
    
    print("NNUE Chess Engine - FEN Evaluator", file=sys.stderr)
    print("Enter FEN strings (one per line), Ctrl+C to exit:", file=sys.stderr)
    print("", file=sys.stderr)
    
    try:
        for line in sys.stdin:
            fen = line.strip()
            if not fen:
                continue
                
            evaluation = evaluate_fen(model, fen)
            if evaluation is not None:
                # Output format: "FEN: evaluation"
                print(f"{fen}: {evaluation:.6f}")
            else:
                print(f"{fen}: ERROR")
                
    except KeyboardInterrupt:
        print("\nExiting...", file=sys.stderr)
    except Exception as e:
        print(f"Unexpected error: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    import os
    main()