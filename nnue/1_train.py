import os
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import TensorDataset, DataLoader

from data import ChessBitboardDataset, load_csv_data, make_dataloader
from model_information import print_model_summary, save_f32_weights, evaluate_model

class NNUE(nn.Module):
    def __init__(self, input_size: int, hidden_size: int = 32):
        super().__init__()
        self.input_size = input_size
        self.hidden = nn.Linear(input_size, hidden_size)
        self.output = nn.Linear(hidden_size, 1)

    def forward(self, x):
        # Clipped ReLU (CReLU) like activation: clamp between 0 and 1
        x = torch.clamp(self.hidden(x), 0, 1)
        return torch.sigmoid(self.output(x)).squeeze(-1)

def print_tensor_debug(tensor):
    for i in range(tensor.shape[0]):
        if tensor[0] != 0:
            print(f"Index {i}: {tensor[i]}")

if __name__ == "__main__":
    print(f'XPU: {torch.xpu.is_available()}')

    csv_filepath = r"C:\dev\chess-data\dataset.csv"
    hidden_size = 16
    num_epochs = 25
    batch_size = 8192

    path = r"C:\dev\chess-data\Lichess Elite Database\Lichess Elite Database\combined.evals.bin"

# Create dataset directly
    ds = ChessBitboardDataset(path)
    print("Number of positions:", len(ds))


    train_size = int(0.9 * len(ds))
    test_size = len(ds) - train_size

    train_ds, test_ds = torch.utils.data.random_split(ds, [train_size, test_size])

    train_loader = make_dataloader(train_ds, batch_size=batch_size, shuffle=True)
    test_loader = make_dataloader(test_ds, batch_size=batch_size, shuffle=False)

    # Get input size from first batch
    sample_inputs, _ = next(iter(train_loader))
    input_size = sample_inputs.shape[1]

    # # Load data directly from CSV file
    # (train_inputs, train_targets), (test_inputs, test_targets) = load_csv_data(csv_filepath)
   
    # input_size = train_inputs.shape[1]
    model = NNUE(input_size=input_size, hidden_size=hidden_size)
   
    optimizer = optim.Adam(model.parameters(), lr=0.001)
    loss_function = nn.MSELoss()

    print_model_summary(model)

    # train_dataset = TensorDataset(train_inputs, train_targets)
    # train_loader = DataLoader(train_dataset, batch_size=batch_size, shuffle=True)
    # test_dataset = TensorDataset(test_inputs, test_targets)
    # test_loader = DataLoader(test_dataset, batch_size=batch_size, shuffle=False)

    print(f"Training on {len(train_ds)} positions, testing on {len(test_ds)} positions...")
    print("Starting training...")
    # if os.path.exists("nnue_weights_trained.pth"):
    #     model.load_state_dict(torch.load("nnue_weights_trained.pth"))
    #     print("Loaded existing weights from nnue_weights_trained.pth")

    device = torch.device('xpu' if torch.xpu.is_available() else 'cpu')
    model = model.to(device)

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
                torch.save(model.state_dict(), "nnue_weights_trained.pth")
                print(f"  New best test loss! Model saved.")
        else:
            print(f"Epoch [{epoch+1}/{num_epochs}], Train Loss: {train_avg_loss:.4f}")
            
    save_f32_weights(model, "nnue_weights.bin")