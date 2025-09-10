import sqlite3
import numpy as np
import torch

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


class PositionDataset(torch.utils.data.Dataset):
    def __init__(self, data_file, batch_size=256):
        self.conn = sqlite3.connect(data_file)
        self.cursor = self.conn.cursor()
        self.batch_size = batch_size
        # Get total number of rows
        self.cursor.execute("SELECT COUNT(*) FROM positions")
        self._len = self.cursor.fetchone()[0]

    def __len__(self):
        return self._len

    def __getitem__(self, index):
        if isinstance(index, slice):
            start, stop, step = index.indices(self._len)
            if step == 1:
                return self.get_batch(start, stop)
            return self.get_list(list(range(start, stop, step)))
        
        if isinstance(index, list):
            return self.get_list(index)
        if isinstance(index, int):
            return self.get_single(index)
        raise ValueError("Type of %s not supported by __getitem__()" % str(index))

    def get_single(self, idx):
        self.cursor.execute(
            "SELECT * FROM positions WHERE id=?",
            (idx + 1,),  # SQLite id is 1-based
        )
        row = self.cursor.fetchone()
        return torch.tensor(row[1:], dtype=torch.float32)

    def get_list(self, indices):
        placeholders = ",".join("?" for _ in indices)
        self.cursor.execute(
            f"SELECT * FROM positions WHERE id IN ({placeholders})",
            [i + 1 for i in indices],
        )
        rows = self.cursor.fetchall()
        return torch.tensor([row[1:] for row in rows], dtype=torch.float32)
        

    def get_batch(self, start, stop):
        # Efficient batch fetch
        print(f"Fetching rows {start} to {stop-1}")
        
        self.cursor.execute(
            f"SELECT * FROM positions ORDER BY id LIMIT {stop - start} OFFSET ?",
            (start + 1,),
        )
        rows = self.cursor.fetchall()
        return torch.tensor([row[1:] for row in rows], dtype=torch.float32)