import os
import random
import math
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from torch.optim.lr_scheduler import CosineAnnealingLR, LinearLR, SequentialLR

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
def blend_targets(wdl: torch.Tensor, eval_cp: torch.Tensor, wdl_lambda: float, eval_scale: float) -> torch.Tensor:
    """
    Blend game result (WDL) with eval-based probability.
    target = wdl_lambda * wdl + (1 - wdl_lambda) * sigmoid(eval / eval_scale)
    """
    eval_prob = torch.sigmoid(eval_cp / eval_scale)
    return wdl_lambda * wdl + (1.0 - wdl_lambda) * eval_prob


@torch.no_grad()
def evaluate_model(model, loader, device, wdl_lambda: float = 1.0, eval_scale: float = 400.0):
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

    for x, wdl, eval_cp in loader:
        x = x.to(device, non_blocking=True)
        wdl = wdl.to(device, non_blocking=True).float().view(-1, 1)
        eval_cp = eval_cp.to(device, non_blocking=True).float().view(-1, 1)
        
        # Blend targets
        y = blend_targets(wdl, eval_cp, wdl_lambda, eval_scale)

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
    warmup_epochs: int = 1,
    min_lr_ratio: float = 0.01,
    wdl_lambda: float = 1.0,
    eval_scale: float = 400.0,
):
    print(f"\n=== Starting {phase_name} ===")

    optimizer = optim.AdamW(model.parameters(), lr=learning_rate, weight_decay=weight_decay)
    loss_fn = nn.BCEWithLogitsLoss(reduction="sum")  # sum then /N

    # Learning rate scheduler: warmup + cosine annealing
    min_lr = learning_rate * min_lr_ratio
    if warmup_epochs > 0 and num_epochs > warmup_epochs:
        warmup_scheduler = LinearLR(
            optimizer,
            start_factor=0.1,
            end_factor=1.0,
            total_iters=warmup_epochs,
        )
        cosine_scheduler = CosineAnnealingLR(
            optimizer,
            T_max=num_epochs - warmup_epochs,
            eta_min=min_lr,
        )
        scheduler = SequentialLR(
            optimizer,
            schedulers=[warmup_scheduler, cosine_scheduler],
            milestones=[warmup_epochs],
        )
        print(f"  Using warmup ({warmup_epochs} epochs) + cosine annealing (min_lr={min_lr:.2e})")
    else:
        scheduler = CosineAnnealingLR(optimizer, T_max=num_epochs, eta_min=min_lr)
        print(f"  Using cosine annealing (min_lr={min_lr:.2e})")

    best_test_bce = float("inf")

    print(f"  Target blend: {wdl_lambda:.0%} WDL + {1-wdl_lambda:.0%} eval (scale={eval_scale})")

    for epoch in range(1, num_epochs + 1):
        model.train()
        total_bce = 0.0
        total_samples = 0
        current_lr = optimizer.param_groups[0]['lr']

        for x, wdl, eval_cp in train_loader:
            x = x.to(device, non_blocking=True)
            wdl = wdl.to(device, non_blocking=True).float().view(-1, 1)
            eval_cp = eval_cp.to(device, non_blocking=True).float().view(-1, 1)
            
            # Blend targets
            y = blend_targets(wdl, eval_cp, wdl_lambda, eval_scale)

            optimizer.zero_grad(set_to_none=True)
            logits = model(x)

            loss = loss_fn(logits, y)  # summed
            loss.backward()

            if grad_clip is not None and grad_clip > 0:
                torch.nn.utils.clip_grad_norm_(model.parameters(), grad_clip)

            optimizer.step()

            total_bce += loss.item()
            total_samples += y.numel()

        # Step the scheduler after each epoch
        scheduler.step()

        train_bce = total_bce / total_samples

        # Evaluate every epoch (your epochs=5 anyway)
        test_bce, test_mse, baseline_mse, r2 = evaluate_model(model, test_loader, device, wdl_lambda, eval_scale)

        print(f"Epoch [{epoch}/{num_epochs}] (lr={current_lr:.2e})")
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

    hidden_size = 32
    batch_size = 8192
    epochs = 25
    lr = 3e-3  # safer default than 1e-2 for AdamW
    
    # Blending parameters: target = wdl_lambda * wdl + (1 - wdl_lambda) * sigmoid(eval / eval_scale)
    wdl_lambda = 0.7  # 0.0 = pure eval, 1.0 = pure game result
    eval_scale = 400.0  # scale factor for eval -> probability conversion

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

    sample_x, _, _ = train_ds[0]
    input_size = int(sample_x.shape[0])

    model = NNUE(input_size=input_size, hidden_size=hidden_size)
    
    # Load existing weights if available
    weights_path = "nnue_weights_final.pth"
    if os.path.exists(weights_path):
        print(f"Loading existing weights from {weights_path}...")
        model.load_state_dict(torch.load(weights_path, weights_only=True))
        print("Weights loaded successfully!")
    else:
        print("No existing weights found, starting from scratch.")
    
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
        wdl_lambda=wdl_lambda,
        eval_scale=eval_scale,
    )

    print("\n" + "=" * 60)
    print("TRAINING COMPLETE")
    print("=" * 60)
    print(f"Best test BCE: {best_bce:.6f}")

    save_f32_weights(model, "nnue_weights.bin")
    torch.save(model.state_dict(), "nnue_weights_final.pth")
    print("Final model saved to nnue_weights.bin and nnue_weights_final.pth")
