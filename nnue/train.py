
import torch
import torch.nn as nn
import torch.optim as optim
import os
import numpy as np  # .npy dataset support
from torch.utils.data import TensorDataset, DataLoader

"""
Minimal NNUE-style network.

Preprocessing is performed externally. This script only:
    * Loads a preprocessed .npy dataset
    * Trains a compact network (input_size inferred from dataset)
    * Exports weights for C# (text + binary)

Expected .npy format:
    - 2D float32 array shape (N, F+1) where last column is target (WDL 0..1)
"""

# Define the NNUE architecture following https://www.dogeystamp.com/chess6/
class NNUE(nn.Module):
    def __init__(self, input_size: int, hidden_size: int = 32):
        super().__init__()
        self.input_size = input_size
        self.hidden = nn.Linear(input_size, hidden_size)
        self.output = nn.Linear(hidden_size, 1)

    def forward(self, x):
        # Clipped ReLU (CReLU) like activation: clamp between 0 and 1
        x = torch.clamp(self.hidden(x), 0, 1)
        return torch.sigmoid(self.output(x))

def print_dataset_info(train_inputs, train_targets, test_inputs, test_targets):
    print("\n=== Dataset Info ===")
    print(f"Train set: {train_inputs.shape[0]} samples, {train_inputs.shape[1]} features")
    print(f"Test set:  {test_inputs.shape[0]} samples, {test_inputs.shape[1]} features")
    all_targets = torch.cat([train_targets, test_targets], dim=0)
    print(f"Targets: mean={all_targets.mean().item():.4f}, std={all_targets.std().item():.4f}, min={all_targets.min().item():.4f}, max={all_targets.max().item():.4f}")
    print("\nFirst 10 training samples:")
    for i in range(min(10, train_inputs.shape[0])):
        input_str = np.array2string(train_inputs[i].numpy(), precision=3, separator=',', suppress_small=True)
        target_val = train_targets[i].item()
        print(f"[{i}] Input: {input_str} | Target: {target_val:.4f}")

def load_csv_data(filepath, train_test_split=0.8):
    """
    Load preprocessed training data from a CSV file.
    Expected format: rows of float values, last column is target.
    Returns: (train_inputs, train_targets), (test_inputs, test_targets)
    """
    print(f"Loading CSV data from {filepath} ...")
    arr = np.loadtxt(filepath, delimiter=',', dtype=np.float32)
    if arr.ndim != 2:
        raise ValueError(f"Unsupported CSV shape {arr.shape}; expected 2D array")
    n_rows, n_cols = arr.shape
    inputs_np = arr[:, :n_cols-1]
    targets_np = arr[:, n_cols-1:n_cols]
    all_inputs = torch.from_numpy(inputs_np)
    all_targets = torch.from_numpy(targets_np)
    indices = torch.randperm(all_inputs.shape[0])
    all_inputs = all_inputs[indices]
    all_targets = all_targets[indices]
    train_size = int(all_inputs.shape[0] * train_test_split)
    train_inputs = all_inputs[:train_size]
    train_targets = all_targets[:train_size]
    test_inputs = all_inputs[train_size:]
    test_targets = all_targets[train_size:]
    print(f"Loaded {all_inputs.shape[0]} samples from CSV (train={train_inputs.shape[0]}, test={test_inputs.shape[0]})")
    return (train_inputs, train_targets), (test_inputs, test_targets)

def evaluate_model(model, test_loader, device='cpu'):
    """
    Evaluate the model on test data and return metrics.
    """
    model.eval()
    total_loss = 0
    total_samples = 0
    correct_predictions = 0  # For win/loss classification
    
    loss_function = nn.MSELoss()
    
    with torch.no_grad():
        for batch_inputs, batch_targets in test_loader:
            batch_inputs = batch_inputs.to(device)
            batch_targets = batch_targets.to(device)
            
            outputs = model(batch_inputs)
            loss = loss_function(outputs, batch_targets)
            
            total_loss += loss.item() * len(batch_inputs)
            total_samples += len(batch_inputs)
            
            # Count "correct" predictions (within 0.1 WDL units)
            correct_predictions += torch.sum(torch.abs(outputs - batch_targets) < 0.1).item()
    
    avg_loss = total_loss / total_samples
    accuracy = correct_predictions / total_samples
    
    return avg_loss, accuracy


def save_weights_binary_for_csharp(model, filename="nnue_weights.bin"):
    """
    Saves the trained model weights in a binary format for faster loading in C#.
    The binary format: all weights as 32-bit floats in the order expected by C#.
    """
    import struct
    
    print(f"Saving weights in binary format to {filename}...")
    
    with open(filename, 'wb') as f:
        # Save hidden weights
        hidden_weights = model.hidden.weight.data.flatten()
        for weight in hidden_weights:
            f.write(struct.pack('f', weight.item()))
        
        # Save hidden bias
        hidden_bias = model.hidden.bias.data
        for bias in hidden_bias:
            f.write(struct.pack('f', bias.item()))
        
        # Save output weights
        output_weights = model.output.weight.data.flatten()
        for weight in output_weights:
            f.write(struct.pack('f', weight.item()))
        
        # Save output bias
        output_bias = model.output.bias.data
        f.write(struct.pack('f', output_bias.item()))
    
    print(f"Binary weights saved to {filename}")
    print("Binary format: 32-bit floats in order: hidden_weights, hidden_bias, output_weights, output_bias")

def print_model_summary(model: NNUE):
    total_params = sum(p.numel() for p in model.parameters())
    hidden_params = model.hidden.weight.numel() + model.hidden.bias.numel()
    output_params = model.output.weight.numel() + model.output.bias.numel()
    print("=== Model Summary ===")
    print(f"Input features: {model.input_size}")
    print(f"Hidden size:    {model.hidden.out_features}")
    print("Activation:     Clipped ReLU (0,1)")
    print("Output:         Sigmoid (WDL prob)")
    print("Parameters:")
    print(f"  Hidden: {hidden_params:,}")
    print(f"  Output: {output_params:,}")
    print(f"  Total:  {total_params:,}")
    print("=====================")

# --- Training Loop ---
if __name__ == "__main__":
    # --- Dataset Setup ---
    csv_filepath = r"C:\dev\chess-data\dataset.csv"
    hidden_size = 16
    num_epochs = 100
    batch_size = 256

    # Load data directly from CSV file
    (train_inputs, train_targets), (test_inputs, test_targets) = load_csv_data(csv_filepath)

    print_dataset_info(train_inputs, train_targets, test_inputs, test_targets)

    input_size = train_inputs.shape[1]
    model = NNUE(input_size=input_size, hidden_size=hidden_size)
    print_model_summary(model)

    optimizer = optim.Adam(model.parameters(), lr=0.001)
    loss_function = nn.MSELoss()

    train_dataset = TensorDataset(train_inputs, train_targets)
    train_loader = DataLoader(train_dataset, batch_size=batch_size, shuffle=True)
    test_dataset = TensorDataset(test_inputs, test_targets)
    test_loader = DataLoader(test_dataset, batch_size=batch_size, shuffle=False)

    print(f"Training on {len(train_inputs)} positions, testing on {len(test_inputs)} positions...")
    print("Starting training...")

    best_test_loss = float('inf')
    for epoch in range(num_epochs):
        model.train()
        total_loss = 0
        num_batches = 0
        for batch_inputs, batch_targets in train_loader:
            optimizer.zero_grad()
            outputs = model(batch_inputs)
            loss = loss_function(outputs, batch_targets)
            loss.backward()
            optimizer.step()
            total_loss += loss.item()
            num_batches += 1
        train_avg_loss = total_loss / num_batches
        if (epoch + 1) % 5 == 0 or epoch == num_epochs - 1:
            test_loss, test_accuracy = evaluate_model(model, test_loader)
            print(f"Epoch [{epoch+1}/{num_epochs}]")
            print(f"  Train Loss: {train_avg_loss:.4f}")
            print(f"  Test Loss: {test_loss:.4f}")
            print(f"  Test Accuracy: {test_accuracy:.3f}")
            if test_loss < best_test_loss:
                best_test_loss = test_loss
                torch.save(model.state_dict(), "nnue_weights_best.pth")
                print(f"  New best test loss! Model saved.")
        else:
            print(f"Epoch [{epoch+1}/{num_epochs}], Train Loss: {train_avg_loss:.4f}")

    print("Training finished.")

    print("\n=== Final Model Evaluation ===")
    final_test_loss, final_test_accuracy = evaluate_model(model, test_loader)
    print(f"Final Test Loss: {final_test_loss:.4f}")
    print(f"Final Test Accuracy: {final_test_accuracy:.3f}")

    torch.save(model.state_dict(), "nnue_weights.pth")
    print("Final model weights saved to nnue_weights.pth")

    if os.path.exists("nnue_weights_best.pth"):
        print("\n=== Best Model Evaluation ===")
        best_model = NNUE(input_size=input_size, hidden_size=hidden_size)
        best_model.load_state_dict(torch.load("nnue_weights_best.pth", weights_only=True))
        best_test_loss, best_test_accuracy = evaluate_model(best_model, test_loader)
        print(f"Best Test Loss: {best_test_loss:.4f}")
        print(f"Best Test Accuracy: {best_test_accuracy:.3f}")
        model = best_model

    save_weights_binary_for_csharp(model, "nnue_weights.bin")

    print("\n--- Example Evaluation (first test sample) ---")
    eval_model = NNUE(input_size=input_size, hidden_size=hidden_size)
    eval_model.load_state_dict(torch.load("nnue_weights.pth", weights_only=True))
    eval_model.eval()
    with torch.no_grad():
        sample_input = test_inputs[0].unsqueeze(0)
        win_probability = eval_model(sample_input)[0,0].item()
        print(f"Sample win probability: {win_probability:.4f}")
