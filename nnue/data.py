
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
        rec = self.mm[idx]
        x = _planes_from_record(rec)
        y = np.float32(rec["wdl_f32"])
        y = 1.0 - y if rec["stm"] == 0 else y
        
        x_t = torch.from_numpy(x)
        y_t = torch.tensor([y], dtype=torch.float32)
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
