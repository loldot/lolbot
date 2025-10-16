
from __future__ import annotations
import os
from typing import Optional, Sequence
import numpy as np
import torch
from torch.utils.data import Dataset, DataLoader

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
    inputs_np = arr[:, :n_cols-2]
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
    print(f"Input size: {train_inputs.shape[1]}, Target size: {train_targets.shape[1]}")
    print("First 10 rows of training data:")
    print(train_inputs[:10])
    print("First 10 rows of training targets:")
    print(train_targets[:10])
    return (train_inputs, train_targets), (test_inputs, test_targets)



RECORD_SIZE = 73  # bytes

# Structured dtype exactly matching your layout (no alignment/padding).
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
    "offsets": [  # byte offsets you provided
        0, 8, 16, 24, 32, 40, 48, 56,
        64, 65, 66, 67, 69
    ],
    "itemsize": RECORD_SIZE
}, align=False)

def _u64_to_bits_le(u64: np.uint64) -> np.ndarray:
    """
    Convert uint64 bitboard into a (64,) uint8 array of 0/1 with LSB -> index 0.
    Uses unpackbits on the 8 constituent bytes (little-endian bit order).
    """
    # view as 8 bytes, unpack to 64 bits (0/1), LSB-first within each byte
    b = np.frombuffer(np.uint64(u64).tobytes(), dtype=np.uint8)
    # bitorder='little' makes bit 0 of each byte go first
    return np.unpackbits(b, bitorder="little")

def _planes_from_record(rec) -> np.ndarray:
    """
    Build the 12x64 feature planes in the order:
    BP, WP, BN, WN, BB, WB, BR, WR, BQ, WQ, BK, WK.
    Returns (768,) float32.
    """
    B = rec["bb_black"]
    W = rec["bb_white"]

    P = rec["bb_pawns"]
    N = rec["bb_knights"]
    Bp = rec["bb_bishops"]
    R = rec["bb_rooks"]
    Q = rec["bb_queens"]
    K = rec["bb_kings"]

    # Mask piece-type bitboards by color
    chans = (
        (P & B), (P & W),
        (N & B), (N & W),
        (Bp & B), (Bp & W),
        (R & B), (R & W),
        (Q & B), (Q & W),
        (K & B), (K & W),
    )

    # Convert each masked u64 to 64 bits and stack
    planes = np.empty((12, 64), dtype=np.uint8)
    for i, bb in enumerate(chans):
        planes[i] = _u64_to_bits_le(bb)

    # Flatten to (768,) float32
    return planes.reshape(-1).astype(np.float32, copy=False)

class ChessBitboardDataset(Dataset):
    """
    Memory-mapped dataset for 73-byte chess records.

    Features: 768-dim one-hot vector ordered as:
      BP, WP, BN, WN, BB, WB, BR, WR, BQ, WQ, BK, WK (each 64 squares).
    Label: float32 WDL at offset 69.
    """
    def __init__(self, path: str | os.PathLike):
        self.path = os.fspath(path)
        size = os.path.getsize(self.path)
        if size % RECORD_SIZE != 0:
            raise ValueError(f"File size {size} not divisible by record size {RECORD_SIZE}.")
        self.n = size // RECORD_SIZE

        # Structured memmap for zero-copy indexed reads.
        self.mm = np.memmap(self.path, dtype=record_dtype, mode="r")

    def __len__(self) -> int:
        return self.n

    def __getitem__(self, idx: int):
        rec = self.mm[idx]  # structured scalar
        x = _planes_from_record(rec)                     # (768,) float32
        y = np.float32(rec["wdl_f32"])                   # label
        # Return torch tensors without extra copies
        x_t = torch.from_numpy(x)                        # float32
        y_t = torch.tensor(y, dtype=torch.float32)
        return x_t, y_t

def make_dataloader(
    ds: Dataset,
    batch_size: int = 8192,
    num_workers: int = 4,
    prefetch_factor: int = 4,
    pin_memory: bool = True,
    persistent_workers: bool = True,
    shuffle: bool = False,
) -> DataLoader:
    return DataLoader(
        ds,
        batch_size=batch_size,
        num_workers=num_workers,
        pin_memory=pin_memory,
        prefetch_factor=prefetch_factor if num_workers > 0 else None,
        persistent_workers=persistent_workers if num_workers > 0 else False,
        shuffle=shuffle,
        drop_last=False,
    )
