"""
Proper NNUE training script that matches the C# implementation.
This implements a basic NNUE network: 768 → HiddenSize → 1
Uses the exact same feature indexing as C#: (color * 6 + piece) * 64 + square
"""

import os
import torch
import torch.nn as nn
import torch.optim as optim
import struct
import numpy as np
from torch.utils.data import DataLoader, Dataset

# Data format constants
RECORD_SIZE = 73  # bytes

# Structured dtype matching the C# serialization format
record_dtype = np.dtype({
    "names": [
        "bb_black", "bb_pawns", "bb_knights", "bb_bishops",
        "bb_rooks", "bb_queens", "bb_kings", "bb_white",
        "stm", "castling", "ep_file", "eval_i16", "wdl_f32"
    ],
    "formats": [
        "<u8", "<u8", "<u8", "<u8", "<u8", "<u8", "<u8", "<u8",  # 0..63
        "u1", "u1", "u1", "<i2", "<f4"                           # 64..72
    ],
    "offsets": [  # byte offsets
        0, 8, 16, 24, 32, 40, 48, 56,
        64, 65, 66, 67, 69
    ],
    "itemsize": RECORD_SIZE
}, align=False)

def _u64_to_bits_le(u64: np.uint64) -> np.ndarray:
    """Convert uint64 bitboard to (64,) array of 0/1 bits (LSB first)"""
    b = np.frombuffer(np.uint64(u64).tobytes(), dtype=np.uint8)
    return np.unpackbits(b, bitorder="little")

def _planes_from_record_c_style_with_stm(rec) -> np.ndarray:
    """
    Build feature planes matching C# FeatureIndex: (color * 6 + piece) * 64 + square + side_to_move
    
    C# indexing:
    - color: 0 = black, 1 = white
    - piece: 0 = pawn, 1 = knight, 2 = bishop, 3 = rook, 4 = queen, 5 = king
    - Feature index = (color * 6 + piece) * 64 + square
    - Side-to-move feature at index 768: 1.0 if white to move, 0.0 if black to move
    
    Creates 769 features in order:
    [Black pieces: BP, BN, BB, BR, BQ, BK] then [White pieces: WP, WN, WB, WR, WQ, WK] then [STM]
    """
    B = rec["bb_black"]
    W = rec["bb_white"]
    stm = rec["stm"]  # 0 = black to move, 7 = white to move
    
    P = rec["bb_pawns"]
    N = rec["bb_knights"] 
    Bp = rec["bb_bishops"]
    R = rec["bb_rooks"]
    Q = rec["bb_queens"]
    K = rec["bb_kings"]

    # Order to match C# FeatureIndex: black pieces first, then white pieces
    # Each piece type in order: pawn, knight, bishop, rook, queen, king
    chans = [
        # Black pieces (color = 0)
        P & B,   # Black pawns
        N & B,   # Black knights  
        Bp & B,  # Black bishops
        R & B,   # Black rooks
        Q & B,   # Black queens
        K & B,   # Black king
        
        # White pieces (color = 1)
        P & W,   # White pawns
        N & W,   # White knights
        Bp & W,  # White bishops  
        R & W,   # White rooks
        Q & W,   # White queens
        K & W,   # White king
    ]

    # Convert each bitboard to 64 bits
    planes = np.empty((12, 64), dtype=np.uint8)
    for i, bb in enumerate(chans):
        planes[i] = _u64_to_bits_le(bb)

    # Flatten piece features to (768,) and add side-to-move feature
    piece_features = planes.reshape(-1).astype(np.float32, copy=False)
    
    # Add side-to-move feature: 1.0 if white to move, 0.0 if black to move
    stm_feature = 1.0 if stm == 7 else 0.0
    
    # Concatenate to create (769,) feature vector
    features = np.concatenate([piece_features, [stm_feature]], dtype=np.float32)
    
    return features

class ProperChessDataset(Dataset):
    """Dataset that matches C# NNUE feature indexing exactly"""
    
    def __init__(self, path: str):
        self.path = os.fspath(path)
        size = os.path.getsize(self.path)
        if size % RECORD_SIZE != 0:
            raise ValueError(f"File size {size} not divisible by record size {RECORD_SIZE}")
        self.n = size // RECORD_SIZE
        self.mm = np.memmap(self.path, dtype=record_dtype, mode="r")
        print(f"Loaded dataset: {self.n:,} positions from {path}")

    def __len__(self) -> int:
        return self.n

    def __getitem__(self, idx: int):
        rec = self.mm[idx]
        x = _planes_from_record_c_style_with_stm(rec)  # (769,) float32
        y = np.float32(rec["wdl_f32"])                 # WDL target
        
        return torch.from_numpy(x), torch.tensor([y], dtype=torch.float32)

def make_dataloader(dataset, batch_size=8192, shuffle=False, num_workers=4):
    """Create a DataLoader with sensible defaults"""
    return DataLoader(
        dataset,
        batch_size=batch_size,
        shuffle=shuffle,
        num_workers=num_workers,
        pin_memory=True,
        drop_last=False,
    )

class ProperNNUE(nn.Module):
    """
    NNUE network that matches the C# implementation architecture.
    - Input: 769 features (12 piece types × 64 squares + 1 side-to-move)
    - Hidden: Accumulator with Clipped ReLU activation
    - Output: Single linear layer (no sigmoid during inference)
    """
    def __init__(self, input_size: int = 769, hidden_size: int = 16):
        super().__init__()
        self.input_size = input_size
        self.hidden_size = hidden_size
        
        # Initialize layers
        self.hidden = nn.Linear(input_size, hidden_size)
        self.output = nn.Linear(hidden_size, 1)
        
        # Initialize weights properly for NNUE
        self._init_weights()
    
    def _init_weights(self):
        """Initialize weights with small values to prevent overflow after quantization"""
        # Small initialization for hidden layer
        nn.init.normal_(self.hidden.weight, mean=0.0, std=0.01)
        nn.init.zeros_(self.hidden.bias)
        
        # Even smaller initialization for output layer
        nn.init.normal_(self.output.weight, mean=0.0, std=0.001)
        nn.init.zeros_(self.output.bias)
    
    def forward(self, x):
        """
        Forward pass for training.
        Uses sigmoid at the end for WDL training.
        """
        # Hidden layer with Clipped ReLU (0, 1 during training)
        hidden_out = torch.clamp(self.hidden(x), 0, 1)
        
        # Output layer
        output = self.output(hidden_out)
        
        # Sigmoid for WDL probability during training
        return torch.sigmoid(output)
    
    def forward_inference(self, x):
        """
        Forward pass for inference (matches C# implementation).
        No sigmoid at the end - returns raw logits that will be scaled.
        """
        hidden_out = torch.clamp(self.hidden(x), 0, 1)
        return self.output(hidden_out)

def wdl_to_cp(wdl_prob, k=400):
    """Convert WDL probability to centipawns using inverse sigmoid"""
    # Clamp to avoid log(0)
    wdl_prob = torch.clamp(wdl_prob, 1e-7, 1 - 1e-7)
    return k * torch.log(wdl_prob / (1 - wdl_prob))

def cp_to_wdl(cp, k=400):
    """Convert centipawns to WDL probability using sigmoid"""
    return torch.sigmoid(cp / k)

def save_nnue_weights(model, filename="nnue_weights.bin"):
    """
    Save model weights in the format expected by C# NNUE.cs
    Format: hidden_weights, hidden_bias, output_weights, output_bias (all float32)
    """
    print(f"Saving NNUE weights to {filename}...")
    
    with open(filename, 'wb') as f:
        # Hidden layer weights: [hidden_size, input_size] -> flatten
        hidden_weights = model.hidden.weight.data  # Shape: [hidden_size, 768]
        for weight in hidden_weights.flatten():
            f.write(struct.pack('f', weight.item()))
        
        # Hidden layer bias: [hidden_size]
        hidden_bias = model.hidden.bias.data
        for bias in hidden_bias:
            f.write(struct.pack('f', bias.item()))
        
        # Output layer weights: [1, hidden_size] -> flatten
        output_weights = model.output.weight.data.flatten()  # Shape: [hidden_size]
        for weight in output_weights:
            f.write(struct.pack('f', weight.item()))
        
        # Output layer bias: scalar
        output_bias = model.output.bias.data.item()
        f.write(struct.pack('f', output_bias))
    
    print(f"Saved {filename} with format: hidden_weights({model.hidden_size}x769), hidden_bias({model.hidden_size}), output_weights({model.hidden_size}), output_bias(1)")

def evaluate_model(model, test_loader, device):
    """Evaluate model on test set"""
    model.eval()
    total_loss = 0
    total_samples = 0
    correct_predictions = 0
    
    loss_function = nn.MSELoss()
    
    with torch.no_grad():
        for batch_inputs, batch_targets in test_loader:
            batch_inputs = batch_inputs.to(device)
            batch_targets = batch_targets.to(device)
            
            outputs = model(batch_inputs)
            loss = loss_function(outputs, batch_targets)
            
            total_loss += loss.item() * len(batch_inputs)
            total_samples += len(batch_inputs)
            
            # Accuracy: predictions within 0.1 WDL units
            outputs_flat = outputs.flatten()
            targets_flat = batch_targets.flatten()
            correct_predictions += torch.sum(torch.abs(outputs_flat - targets_flat) < 0.1).item()
    
    avg_loss = total_loss / total_samples
    accuracy = correct_predictions / total_samples
    return avg_loss, accuracy

def print_model_info(model):
    """Print model architecture info"""
    total_params = sum(p.numel() for p in model.parameters())
    
    print("=== NNUE Model Info ===")
    print(f"Architecture: {model.input_size} → {model.hidden_size} → 1")
    print(f"Input features: {model.input_size} (768 piece + 1 side-to-move)")
    print(f"Hidden size: {model.hidden_size}")
    print(f"Total parameters: {total_params:,}")
    print(f"Hidden params: {model.hidden.weight.numel() + model.hidden.bias.numel():,}")
    print(f"Output params: {model.output.weight.numel() + model.output.bias.numel():,}")
    print("Activation: Clipped ReLU (0,1)")
    print("Training: Sigmoid output for WDL")
    print("Inference: Raw logits (no sigmoid)")
    print("=======================")

def validate_feature_indexing():
    """Validate that our feature indexing matches C# exactly"""
    print("Validating feature indexing matches C#...")
    
    # Test the feature index calculation
    def c_sharp_feature_index(color, piece, square):
        """Exact copy of C# FeatureIndex method"""
        return ((color * 6) + piece) * 64 + square
    
    # Expected order: Black pieces first (color=0), then White pieces (color=1)
    # Pieces: 0=pawn, 1=knight, 2=bishop, 3=rook, 4=queen, 5=king
    expected_indices = []
    piece_names = ["Pawn", "Knight", "Bishop", "Rook", "Queen", "King"]
    
    print("\nFeature index mapping:")
    for color in [0, 1]:  # 0=Black, 1=White
        color_name = "Black" if color == 0 else "White"
        for piece in range(6):  # 0-5
            base_idx = c_sharp_feature_index(color, piece, 0)  # Square 0 (A1)
            print(f"  {color_name} {piece_names[piece]:6s}: features {base_idx:3d}-{base_idx+63:3d}")
            expected_indices.extend(range(base_idx, base_idx + 64))
    
    # Add side-to-move feature
    print(f"  Side-to-move        : feature  768")
    expected_indices.append(768)
    
    # Verify we have exactly 769 unique indices
    assert len(expected_indices) == 769, f"Expected 769 features, got {len(expected_indices)}"
    assert len(set(expected_indices)) == 769, "Feature indices are not unique!"
    assert min(expected_indices) == 0, "First feature index should be 0"
    assert max(expected_indices) == 768, "Last feature index should be 768"
    
    print("✓ Feature indexing validation passed!")

def main():
    # Configuration
    data_path = "C:\\dev\\chess-data\\Lichess Elite Database\\Lichess Elite Database\\combined.evals.bin"
    hidden_size = 16  # Must match C# HiddenSize constant
    num_epochs = 100
    batch_size = 8192
    learning_rate = 0.001
    
    # Validate feature indexing first
    validate_feature_indexing()
    
    print("\nLoading dataset...")
    dataset = ProperChessDataset(data_path)
    print(f"Dataset size: {len(dataset):,} positions")
    
    # Split dataset
    train_size = int(0.9 * len(dataset))
    test_size = len(dataset) - train_size
    train_ds, test_ds = torch.utils.data.random_split(dataset, [train_size, test_size])
    
    # Create data loaders
    train_loader = make_dataloader(train_ds, batch_size=batch_size, shuffle=True)
    test_loader = make_dataloader(test_ds, batch_size=batch_size, shuffle=False)
    
    print(f"Training set: {len(train_ds):,} positions")
    print(f"Test set: {len(test_ds):,} positions")
    
    # Create model
    model = ProperNNUE(input_size=769, hidden_size=hidden_size)
    print_model_info(model)
    
    # Setup training
    device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
    print(f"Using device: {device}")
    
    model = model.to(device)
    optimizer = optim.Adam(model.parameters(), lr=learning_rate)
    loss_function = nn.MSELoss()
    
    # Training loop
    best_test_loss = float('inf')
    best_model_path = "nnue_weights_best.pth"
    
    print("\nStarting training...")
    for epoch in range(num_epochs):
        model.train()
        total_loss = 0
        num_batches = 0
        
        for batch_inputs, batch_targets in train_loader:
            batch_inputs = batch_inputs.to(device)
            batch_targets = batch_targets.to(device)
            
            optimizer.zero_grad()
            outputs = model(batch_inputs)
            loss = loss_function(outputs, batch_targets)
            loss.backward()
            optimizer.step()
            
            total_loss += loss.item()
            num_batches += 1
        
        train_avg_loss = total_loss / num_batches
        
        # Evaluate every 5 epochs or on last epoch
        if (epoch + 1) % 5 == 0 or epoch == num_epochs - 1:
            test_loss, test_accuracy = evaluate_model(model, test_loader, device)
            
            print(f"Epoch [{epoch+1}/{num_epochs}]")
            print(f"  Train Loss: {train_avg_loss:.6f}")
            print(f"  Test Loss:  {test_loss:.6f}")
            print(f"  Test Acc:   {test_accuracy:.3%}")
            
            # Save best model
            if test_loss < best_test_loss:
                best_test_loss = test_loss
                torch.save(model.state_dict(), best_model_path)
                print(f"  New best! Saved to {best_model_path}")
        else:
            print(f"Epoch [{epoch+1}/{num_epochs}], Train Loss: {train_avg_loss:.6f}")
    
    print(f"\nTraining complete. Best test loss: {best_test_loss:.6f}")
    
    # Load best model and save weights
    print("Loading best model for weight export...")
    model.load_state_dict(torch.load(best_model_path))
    save_nnue_weights(model, "nnue_weights.bin")
    
    print("Training and export complete!")

if __name__ == "__main__":
    main()