import os
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import TensorDataset, DataLoader, Dataset
import numpy as np

from data import ChessBitboardDataset, make_dataloader, record_dtype
from model_information import print_model_summary, save_f32_weights, evaluate_model

class NNUE(nn.Module):
    def __init__(self, input_size: int, hidden_size: int = 32):
        super().__init__()
        self.input_size = input_size
        self.hidden = nn.Linear(input_size, hidden_size, dtype=torch.float32)
        self.output = nn.Linear(hidden_size, 1, dtype=torch.float32)

    def forward(self, x):
        # Clipped ReLU (CReLU) like activation: clamp between 0 and 1
        x = torch.clamp(self.hidden(x), 0, 1)
        return torch.sigmoid(self.output(x))

def print_tensor_debug(tensor):
    for i in range(tensor.shape[0]):
        if tensor[0] != 0:
            print(f"Index {i}: {tensor[i]}")

def train_phase(model, train_loader, test_loader, device, phase_name, num_epochs, learning_rate=0.001):
    """
    Train the model for a specific phase.
    """
    print(f"\n=== Starting {phase_name} ===")
    
    optimizer = optim.AdamW(model.parameters(), lr=learning_rate, weight_decay=1e-5)
    loss_function = nn.MSELoss()
    
    best_test_loss = float('inf')
    
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

        if (epoch + 1) % 5 == 0 or epoch == num_epochs - 1:
            test_loss, test_accuracy = evaluate_model(model, test_loader, device)
            print(f"Epoch [{epoch+1}/{num_epochs}]")
            print(f"  Train Loss: {train_avg_loss:.4f}")
            print(f"  Test Loss: {test_loss:.4f}")
            print(f"  Test Accuracy: {test_accuracy:.3f}")
            if test_loss < best_test_loss:
                best_test_loss = test_loss
                # Save phase-specific checkpoint
                checkpoint_name = f"nnue_weights_{phase_name.lower().replace(' ', '_')}.pth"
                torch.save(model.state_dict(), checkpoint_name)
                print(f"  New best test loss! Model saved to {checkpoint_name}")
        else:
            print(f"Epoch [{epoch+1}/{num_epochs}], Train Loss: {train_avg_loss:.4f}")
    
    print(f"=== Completed {phase_name} ===")
    return best_test_loss

if __name__ == "__main__":
    print(f'XPU: {torch.xpu.is_available()}')

    # Training configuration
    hidden_size = 16
    batch_size = 8192
        
    epochs = 5
    lr = 0.05  # Lower learning rate for fine-tuning

    path = r"C:\dev\chess-data\Lichess Elite Database\Lichess Elite Database\preprocessed_positions.bin"

    # Load base dataset
    base_ds = ChessBitboardDataset(path)
    print("Number of positions:", len(base_ds))

    # Split dataset indices
    train_size = int(0.9 * len(base_ds))
    test_size = len(base_ds) - train_size
    
    # Create random indices
    indices = torch.randperm(len(base_ds)).tolist()
    train_indices = indices[:train_size]
    test_indices = indices[train_size:]

    # Get input size from sample
    sample_inputs, _ = base_ds[0]
    input_size = sample_inputs.shape[0]

    # Create model
    model = NNUE(input_size=input_size, hidden_size=hidden_size)
    print_model_summary(model)

    device = torch.device('xpu' if torch.xpu.is_available() else 'cpu')
    model = model.to(device)

    print(f"Training on {train_size} positions, testing on {test_size} positions...")
    
    
    # === PHASE 2: Fine-tuning with Accurate WDL ===
    print("\n" + "="*60)
    print("PHASE 2: Fine-tuning with Accurate WDL")
    print("This refines the evaluation using more nuanced position assessments")
    print("="*60)
    
    # Create accurate WDL datasets (using original dataset)
    accurate_train_ds = torch.utils.data.Subset(base_ds, train_indices)
    accurate_test_ds = torch.utils.data.Subset(base_ds, test_indices)
    
    accurate_train_loader = make_dataloader(accurate_train_ds, batch_size=batch_size, shuffle=True)
    accurate_test_loader = make_dataloader(accurate_test_ds, batch_size=batch_size, shuffle=False)
    
    # Train phase 2 (fine-tuning)
    loss = train_phase(
        model, accurate_train_loader, accurate_test_loader, device,
        "Training WDL", epochs, lr
    )
    
    # === Final Results ===
    print("\n" + "="*60)
    print("TRAINING COMPLETE")
    print("="*60)

    print(f"Best loss: {loss:.6f}")
    
    # Save final weights
    save_f32_weights(model, "nnue_weights.bin")
    torch.save(model.state_dict(), "nnue_weights_final.pth")
    print("Final model saved to nnue_weights.bin and nnue_weights_final.pth")