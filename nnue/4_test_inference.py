"""
NNUE Inference Script
Load trained network weights and run inference on FEN positions.
Uses the same feature encoding as the training script and outputs raw logits * 410.
"""

import torch
import torch.nn as nn
import numpy as np
import struct
from typing import Dict, Tuple

class NNUE(nn.Module):
    """NNUE model matching the training architecture"""
    def __init__(self, input_size: int = 768, hidden_size: int = 16):
        super().__init__()
        self.input_size = input_size
        self.hidden_size = hidden_size
        self.hidden = nn.Linear(input_size, hidden_size)
        self.output = nn.Linear(hidden_size, 1)

    def forward_inference(self, x):
        """
        Forward pass for inference (no sigmoid).
        Returns raw logits that should be multiplied by scaling factor.
        """
        hidden_out = torch.clamp(self.hidden(x), 0, 1)
        return self.output(hidden_out)

def load_model_weights(model, weights_path="nnue_weights_final.pth"):
    """Load weights from PyTorch .pth file"""
    print(f"Loading weights from {weights_path}...")
    
    try:
        # Load PyTorch state dict
        state_dict = torch.load(weights_path, map_location='cpu')
        
        # Load weights into model
        model.load_state_dict(state_dict)
        
        print("Weights loaded successfully!")
        print(f"Model parameters:")
        for name, param in model.named_parameters():
            print(f"  {name}: {param.shape}")
            
    except FileNotFoundError:
        print(f"Error: File {weights_path} not found!")
        raise
    except Exception as e:
        print(f"Error loading weights: {e}")
        raise

def fen_to_bitboards(fen: str) -> Tuple[Dict[str, int], str]:
    """
    Convert FEN string to bitboard representation.
    Returns tuple of (bitboards dict, side_to_move).
    """
    parts = fen.split()
    board_str = parts[0]
    side_to_move = parts[1]
    
    # Initialize bitboards
    bitboards = {
        'white': 0,
        'black': 0,
        'pawns': 0,
        'knights': 0,
        'bishops': 0,
        'rooks': 0,
        'queens': 0,
        'kings': 0
    }
    
    # Piece mapping
    pieces = {
        'P': ('white', 'pawns'), 'p': ('black', 'pawns'),
        'N': ('white', 'knights'), 'n': ('black', 'knights'),
        'B': ('white', 'bishops'), 'b': ('black', 'bishops'),
        'R': ('white', 'rooks'), 'r': ('black', 'rooks'),
        'Q': ('white', 'queens'), 'q': ('black', 'queens'),
        'K': ('white', 'kings'), 'k': ('black', 'kings')
    }
    
    # Parse board position
    rank = 7  # Start from rank 8 (index 7)
    file = 0
    
    for char in board_str:
        if char == '/':
            rank -= 1
            file = 0
        elif char.isdigit():
            file += int(char)
        elif char in pieces:
            square = rank * 8 + file
            bit = 1 << square
            
            color, piece_type = pieces[char]
            bitboards[color] |= bit
            bitboards[piece_type] |= bit
            
            file += 1
    
    return bitboards, side_to_move

def bitboards_to_features(bitboards: Dict[str, int]) -> np.ndarray:
    """
    Convert bitboards to 768-dimensional feature vector.
    Uses the same ordering as the training data: WP, BP, WN, BN, WB, BB, WR, BR, WQ, BQ, WK, BK
    """
    def bitboard_to_array(bb: int) -> np.ndarray:
        """Convert bitboard to 64-element array"""
        return np.array([(bb >> i) & 1 for i in range(64)], dtype=np.float32)
    
    # Extract piece bitboards by color
    white_pawns = bitboards['pawns'] & bitboards['white']
    black_pawns = bitboards['pawns'] & bitboards['black']
    white_knights = bitboards['knights'] & bitboards['white']
    black_knights = bitboards['knights'] & bitboards['black']
    white_bishops = bitboards['bishops'] & bitboards['white']
    black_bishops = bitboards['bishops'] & bitboards['black']
    white_rooks = bitboards['rooks'] & bitboards['white']
    black_rooks = bitboards['rooks'] & bitboards['black']
    white_queens = bitboards['queens'] & bitboards['white']
    black_queens = bitboards['queens'] & bitboards['black']
    white_kings = bitboards['kings'] & bitboards['white']
    black_kings = bitboards['kings'] & bitboards['black']
    
    # Convert to feature arrays in ACTUAL training order: BP, BN, BB, BR, BQ, BK, WP, WN, WB, WR, WQ, WK
    features = np.concatenate([
        bitboard_to_array(black_pawns),
        bitboard_to_array(black_knights),
        bitboard_to_array(black_bishops),
        bitboard_to_array(black_rooks),
        bitboard_to_array(black_queens),
        bitboard_to_array(black_kings),
        bitboard_to_array(white_pawns),
        bitboard_to_array(white_knights),
        bitboard_to_array(white_bishops),
        bitboard_to_array(white_rooks),
        bitboard_to_array(white_queens),
        bitboard_to_array(white_kings),
    ])
    
    return features

def evaluate_fen(model: NNUE, fen: str) -> float:
    """
    Evaluate a FEN position using the trained NNUE model.
    Returns evaluation in centipawns (logits * 410).
    """
    # Convert FEN to features
    bitboards, side_to_move = fen_to_bitboards(fen)
    features = bitboards_to_features(bitboards)
    
    # Convert to tensor and add batch dimension
    input_tensor = torch.from_numpy(features).unsqueeze(0)
    
    # Run inference
    model.eval()
    with torch.no_grad():
        logits = model.forward_inference(input_tensor)
        
        # Multiply by 410 like C# implementation
        evaluation = logits.item() * 410
        
        # Flip sign if black to move (since network is trained from white perspective)
        if side_to_move == 'b':
            evaluation = -evaluation
    
    return evaluation

def test_positions():
    """Test the model on some standard chess positions"""
    
    # Load model
    model = NNUE(input_size=768, hidden_size=16)
    load_model_weights(model, "nnue_weights_final.pth")
    
    # Test positions
    test_fens = [
        ("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", "Starting position"),
        ("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1", "1.e4"),
        ("rnbqkb1r/pppp1ppp/5n2/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 2 3", "1.e4 e5 2.Nf3"),
        ("r1bqkb1r/pppp1ppp/2n2n2/4p3/2B1P3/8/PPPP1PPP/RNBQK1NR w KQkq - 4 4", "Italian Game"),
        ("8/8/8/8/8/8/8/K7 w - - 0 1", "Lone King (White)"),
        ("k7/8/8/8/8/8/8/8 b - - 0 1", "Lone King (Black)"),
    ]
    
    print("Testing NNUE evaluation on standard positions:")
    print("=" * 60)
    
    for fen, description in test_fens:
        try:
            evaluation = evaluate_fen(model, fen)
            print(f"{description:25s}: {evaluation:+8.1f} cp")
        except Exception as e:
            print(f"{description:25s}: ERROR - {e}")
    
    print("=" * 60)

def test_file_positions(filename="test_positions.txt"):
    """Test positions from a file"""
    
    # Load model
    model = NNUE(input_size=768, hidden_size=16)
    try:
        load_model_weights(model, "nnue_weights_final.pth")
    except FileNotFoundError:
        print("Error: nnue_weights_final.pth not found!")
        print("Please train a model first using 1_train.py")
        return
    
    try:
        with open(filename, 'r') as f:
            lines = f.readlines()
        
        print(f"\nEvaluating {len(lines)} positions from {filename}")
        print("=" * 80)
        
        for i, line in enumerate(lines, 1):
            fen = line.strip()
            if not fen or fen.startswith('#'):
                continue
            
            try:
                evaluation = evaluate_fen(model, fen)
                bitboards, side_to_move = fen_to_bitboards(fen)
                side_name = "White" if side_to_move == 'w' else "Black"
                
                print(f"Position {i}: {fen}")
                print(f"  Side to move: {side_name}")
                print(f"  PyTorch Evaluation: {evaluation:+.1f} cp")
                print()
                
            except Exception as e:
                print(f"Position {i}: ERROR - {e}")
                print()
                
    except FileNotFoundError:
        print(f"Error: File {filename} not found!")
    except Exception as e:
        print(f"Error reading file: {e}")

def interactive_mode():
    """Interactive mode for testing FEN positions"""
    
    # Load model
    model = NNUE(input_size=768, hidden_size=16)
    try:
        load_model_weights(model, "nnue_weights_final.pth")
    except FileNotFoundError:
        print("Error: nnue_weights_final.pth not found!")
        print("Please train a model first using 1_train.py")
        return
    
    print("\nNNUE Interactive Evaluation")
    print("Enter FEN strings to evaluate positions")
    print("Type 'quit' to exit\n")
    
    while True:
        try:
            fen = input("FEN> ").strip()
            
            if fen.lower() in ['quit', 'exit', 'q']:
                break
            
            if not fen:
                continue
            
            evaluation = evaluate_fen(model, fen)
            print(f"Evaluation: {evaluation:+.1f} cp\n")
            
        except KeyboardInterrupt:
            print("\nGoodbye!")
            break
        except Exception as e:
            print(f"Error: {e}\n")

if __name__ == "__main__":
    print("NNUE Inference Script")
    print("Loads trained weights and evaluates FEN positions")
    print("Outputs raw logits * 410 (no sigmoid)")
    
    # Run tests on standard positions
    test_positions()
    
    # Test positions from file
    print("\n" + "=" * 80)
    test_file_positions("test_positions.txt")
    
    # Start interactive mode
    interactive_mode()
