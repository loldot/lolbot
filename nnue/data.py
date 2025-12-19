
from __future__ import annotations
import os
from typing import Optional, Sequence
import numpy as np
import torch
from torch.utils.data import Dataset, DataLoader

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
    
    B = rec["bb_black"]
    W = rec["bb_white"]
    
    P = rec["bb_pawns"]
    N = rec["bb_knights"] 
    Bp = rec["bb_bishops"]
    R = rec["bb_rooks"]
    Q = rec["bb_queens"]
    K = rec["bb_kings"]

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
    return planes.reshape(-1).astype(np.float32, copy=False)

class ChessBitboardDataset(Dataset):
    """
    Chess dataset that supports multiprocessing on Windows by deferring memmap creation.
    Each worker opens its own memmap handle via worker_init_fn.
    
    Use start_idx and end_idx for train/test splits instead of Subset (avoids pickling huge index lists).
    """
    def __init__(
        self,
        path: str | os.PathLike,
        start_idx: int = 0,
        end_idx: int | None = None,
        *,
        split_modulus: int | None = None,
        split_remainder_start: int = 0,
        split_remainder_count: int | None = None,
    ):
        self.path = os.fspath(path)
        size = os.path.getsize(self.path)
        if size % RECORD_SIZE != 0:
            raise ValueError(f"File size {size} not divisible by record size {RECORD_SIZE}.")
        self.total_n = size // RECORD_SIZE
        
        # Support slicing for train/test split without Subset
        self.start_idx = start_idx
        self.end_idx = end_idx if end_idx is not None else self.total_n

        if not (0 <= self.start_idx <= self.end_idx <= self.total_n):
            raise ValueError(
                f"Invalid slice: start_idx={self.start_idx}, end_idx={self.end_idx}, total={self.total_n}"
            )

        # Optional modulo-based split.
        # This is critical when the underlying file is not shuffled: it creates an interleaved split
        # without allocating massive index arrays (Windows-friendly).
        self.split_modulus = split_modulus
        self.split_remainder_start = int(split_remainder_start)

        if self.split_modulus is None:
            self.split_remainder_count = None
            self.n = self.end_idx - self.start_idx
        else:
            m = int(self.split_modulus)
            if m <= 0:
                raise ValueError(f"split_modulus must be positive, got {m}.")

            if split_remainder_count is None:
                raise ValueError("split_remainder_count is required when split_modulus is set.")
            k = int(split_remainder_count)
            if k <= 0:
                raise ValueError(f"split_remainder_count must be positive, got {k}.")

            if not (0 <= self.split_remainder_start < m):
                raise ValueError(
                    f"split_remainder_start must be in [0,{m}), got {self.split_remainder_start}."
                )
            if self.split_remainder_start + k > m:
                raise ValueError(
                    f"Invalid remainder window: start={self.split_remainder_start}, count={k}, modulus={m}."
                )

            self.split_remainder_count = k

            base_n = self.end_idx - self.start_idx
            full_blocks = base_n // m
            leftover = base_n % m
            extra = 0
            if leftover > self.split_remainder_start:
                extra = min(k, leftover - self.split_remainder_start)
            self.n = full_blocks * k + extra
        
        # Don't create memmap here - will be created per-worker via worker_init_fn
        # This avoids pickling issues on Windows with large files
        self.mm = None

    def open_memmap(self):
        """Open the memmap. Called by worker_init_fn in each worker process."""
        if self.mm is None:
            self.mm = np.memmap(self.path, dtype=record_dtype, mode="r")

    def __len__(self) -> int:
        return self.n

    def __getitem__(self, idx: int):
        # Ensure memmap is open (handles single-process case where worker_init_fn isn't called)
        if self.mm is None:
            self.open_memmap()

        mm = self.mm
        if mm is None:
            raise RuntimeError("Memmap failed to open")
        
        # Map to actual index in the file
        if self.split_modulus is None:
            actual_idx = self.start_idx + idx
        else:
            # Interleaved selection: keep the window of remainders
            # [split_remainder_start, split_remainder_start + split_remainder_count).
            m = int(self.split_modulus)
            k = self.split_remainder_count
            if k is None:
                raise RuntimeError("split_remainder_count not set")
            k_i = int(k)
            block = idx // k_i
            r = idx % k_i
            actual_idx = self.start_idx + block * m + (self.split_remainder_start + r)

        if actual_idx >= self.end_idx:
            raise IndexError("Index out of range")
        rec = mm[actual_idx]
        x = _planes_from_record(rec)
        y = np.float32(rec["wdl_f32"])
        
        x_t = torch.from_numpy(x)
        y_t = torch.tensor([y], dtype=torch.float32)
        return x_t, y_t

def _worker_init_fn(worker_id):
    """Initialize each worker by opening its own memmap handle."""
    worker_info = torch.utils.data.get_worker_info()
    if worker_info is not None:
        dataset = worker_info.dataset
        # Handle wrappers (e.g., Subset/ConcatDataset) defensively.
        while True:
            inner = getattr(dataset, "dataset", None)
            if inner is None:
                break
            dataset = inner

        open_fn = getattr(dataset, "open_memmap", None)
        if callable(open_fn):
            open_fn()

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
        worker_init_fn=_worker_init_fn if num_workers > 0 else None,
    )
