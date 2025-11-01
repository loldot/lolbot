import torch
import torch.nn as nn

def save_f32_weights(model, filename="nnue_weights.bin"):
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

def print_model_summary(model):

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


def evaluate_model(model, test_loader, device):
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
            # Flatten both to ensure same shape for comparison
            outputs_flat = outputs.flatten()
            targets_flat = batch_targets.flatten()
            correct_predictions += torch.sum(torch.abs(outputs_flat - targets_flat) < 0.1).item()
    
    avg_loss = total_loss / total_samples
    accuracy = correct_predictions / total_samples
    
    return avg_loss, accuracy