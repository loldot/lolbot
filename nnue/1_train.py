import os
import random
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim

from data import ChessBitboardDataset, make_dataloader
from model_information import print_model_summary, save_f32_weights

# --------------------------
# Repro / determinism helpers
# --------------------------
def seed_all(seed: int = 1234):
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    torch.cuda.manual_seed_all(seed) if torch.cuda.is_available() else None

# --------------------------
# Model
# --------------------------
class NNUE(nn.Module):
    """
    Outputs logits (unbounded). Use sigmoid only for metrics/inference.
    """
    def __init__(self, input_size: int, hidden_size: int = 16):
        super().__init__()
        self.input_size = input_size
        self.hidden = nn.Linear(input_size, hidden_size, dtype=torch.float32)
        self.output = nn.Linear(hidden_size, 1, dtype=torch.float32)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        # CReLU-like clamp activation in hidden layer
        x = torch.clamp(self.hidden(x), 0.0, 1.0)
        logits = self.output(x)  # NO sigmoid here
        return logits

# --------------------------
# Evaluation
# --------------------------
@torch.no_grad()
def evaluate_model(model, loader, device):
    """
    Returns:
      - bce_loss (avg per sample)
      - mse (avg per sample) on probabilities
      - baseline_mse (predict mean target) on this loader
      - r2 (on probabilities vs targets)
    """
    model.eval()

    bce = nn.BCEWithLogitsLoss(reduction="sum")  # sum then /N
    total_bce = 0.0

    # For MSE / baseline / R2 we work in probability space
    total_mse = 0.0
    total_targets_sum = 0.0
    total_targets_sq_sum = 0.0
    total_samples = 0

    for x, y in loader:
        x = x.to(device, non_blocking=True)
        y = y.to(device, non_blocking=True).float().view(-1, 1)  # ensure shape [B,1]

        logits = model(x)
        total_bce += bce(logits, y).item()

        p = torch.sigmoid(logits)
        total_mse += torch.sum((p - y) ** 2).item()

        total_targets_sum += torch.sum(y).item()
        total_targets_sq_sum += torch.sum(y ** 2).item()
        total_samples += y.numel()

    avg_bce = total_bce / total_samples
    avg_mse = total_mse / total_samples

    # baseline: predict mean target mu
    mu = total_targets_sum / total_samples
    var = (total_targets_sq_sum / total_samples) - (mu * mu)
    baseline_mse = var  # MSE of constant predictor = variance

    # R^2 in probability space (guard against var ~ 0)
    r2 = 0.0 if baseline_mse <= 1e-12 else (1.0 - (avg_mse / baseline_mse))

    return avg_bce, avg_mse, baseline_mse, r2

# --------------------------
# Training
# --------------------------
def train_phase(
    model,
    train_loader,
    test_loader,
    device,
    phase_name: str,
    num_epochs: int,
    learning_rate: float = 3e-3,
    weight_decay: float = 1e-5,
    grad_clip: float = 1.0,
):
    print(f"\n=== Starting {phase_name} ===")

    optimizer = optim.AdamW(model.parameters(), lr=learning_rate, weight_decay=weight_decay)
    loss_fn = nn.BCEWithLogitsLoss(reduction="sum")  # sum then /N

    best_test_bce = float("inf")

    for epoch in range(1, num_epochs + 1):
        model.train()
        total_bce = 0.0
        total_samples = 0

        for x, y in train_loader:
            x = x.to(device, non_blocking=True)
            y = y.to(device, non_blocking=True).float().view(-1, 1)

            optimizer.zero_grad(set_to_none=True)
            logits = model(x)

            loss = loss_fn(logits, y)  # summed
            loss.backward()

            if grad_clip is not None and grad_clip > 0:
                torch.nn.utils.clip_grad_norm_(model.parameters(), grad_clip)

            optimizer.step()

            total_bce += loss.item()
            total_samples += y.numel()

        train_bce = total_bce / total_samples

        # Evaluate every epoch (your epochs=5 anyway)
        test_bce, test_mse, baseline_mse, r2 = evaluate_model(model, test_loader, device)

        print(f"Epoch [{epoch}/{num_epochs}]")
        print(f"  Train BCE: {train_bce:.6f}")
        print(f"  Test  BCE: {test_bce:.6f}")
        print(f"  Test  MSE(prob): {test_mse:.6f} | baseline MSE: {baseline_mse:.6f} | R^2: {r2:.4f}")

        if test_bce < best_test_bce:
            best_test_bce = test_bce
            checkpoint_name = f"nnue_weights_{phase_name.lower().replace(' ', '_')}.pth"
            torch.save(model.state_dict(), checkpoint_name)
            print(f"  New best test BCE! Saved to {checkpoint_name}")

    print(f"=== Completed {phase_name} ===")
    return best_test_bce

# --------------------------
# Main
# --------------------------
if __name__ == "__main__":
    seed_all(1234)

    print(f"XPU: {torch.xpu.is_available()}")

    hidden_size = 16
    batch_size = 8192
    epochs = 5
    lr = 3e-3  # safer default than 1e-2 for AdamW

    path = r"C:\dev\chess-data\Lichess Elite Database\Lichess Elite Database\preprocessed_positions.bin"

    # Get total size without loading memmap
    import os
    total_positions = os.path.getsize(path) // 73  # RECORD_SIZE = 73
    print(f"Total positions in file: {total_positions:,}")

    # NOTE: The preprocessor currently does NOT shuffle the file; it writes category blocks.
    # A contiguous 90/10 split will therefore create a big distribution shift.
    # Use an interleaved split without huge index lists (Windows-friendly):
    #   train = records where (idx % 10) in [0..8]
    #   test  = records where (idx % 10) == 9
    split_mod = 10
    train_keep = 9
    train_ds = ChessBitboardDataset(
        path,
        start_idx=0,
        end_idx=total_positions,
        split_modulus=split_mod,
        split_remainder_start=0,
        split_remainder_count=train_keep,
    )
    test_ds = ChessBitboardDataset(
        path,
        start_idx=0,
        end_idx=total_positions,
        split_modulus=split_mod,
        split_remainder_start=train_keep,
        split_remainder_count=1,
    )

    train_size = len(train_ds)
    test_size = len(test_ds)

    sample_x, _ = train_ds[0]
    input_size = int(sample_x.shape[0])

    model = NNUE(input_size=input_size, hidden_size=hidden_size)
    print_model_summary(model)

    device = torch.device("xpu" if torch.xpu.is_available() else "cpu")
    model = model.to(device)

    print(f"Training on {train_size:,} positions, testing on {test_size:,} positions...")

    train_loader = make_dataloader(train_ds, batch_size=batch_size, shuffle=True)
    test_loader = make_dataloader(test_ds, batch_size=batch_size, shuffle=False)

    print("\n" + "=" * 60)
    print("PHASE: Training WDL (prob targets in [0,1])")
    print("=" * 60)

    best_bce = train_phase(
        model,
        train_loader,
        test_loader,
        device,
        phase_name="Training WDL",
        num_epochs=epochs,
        learning_rate=lr,
    )

    print("\n" + "=" * 60)
    print("TRAINING COMPLETE")
    print("=" * 60)
    print(f"Best test BCE: {best_bce:.6f}")

    save_f32_weights(model, "nnue_weights.bin")
    torch.save(model.state_dict(), "nnue_weights_final.pth")
    print("Final model saved to nnue_weights.bin and nnue_weights_final.pth")
