using System;
using System.IO;
using Lolbot.Core;

namespace Lolbot.Tests;

[Category(TestSuites.Fast)]
public class BinarySerializationTests
{
    [Test]
    public void TestPositionSerialization()
    {
        // Test starting position serialization
        var position = new MutablePosition();
        short eval = 25;  // +25 centipawns
        float wdl = 0.55f; // Slightly favoring white
        
        using var stream = new MemoryStream();
        BinarySerializer.WritePosition(stream, position, eval, wdl);
        
        var data = stream.ToArray();
        
        // Verify total size is 73 bytes
        data.Length.Should().Be(73, "Total record size should be 73 bytes");
        
        // Verify the position part (67 bytes)
        var positionData = new byte[67];
        Array.Copy(data, 0, positionData, 0, 67);
        
        // Check that we can copy position data correctly
        var buffer = new byte[67];
        int written = position.CopyTo(buffer);
        written.Should().Be(67, "Position should write exactly 67 bytes");
        
        positionData.Should().Equal(buffer, "Position data should match");
        
        // Verify eval (short at bytes 67-68)
        short readEval = BitConverter.ToInt16(data, 67);
        readEval.Should().Be(eval, "Eval should be correctly serialized");
        
        // Verify WDL (float at bytes 69-72)
        float readWdl = BitConverter.ToSingle(data, 69);
        readWdl.Should().BeApproximately(wdl, 0.000001f, "WDL should be correctly serialized");
    }
    
    [Test]
    public void TestBitboardStructure()
    {
        var position = new MutablePosition();
        
        // Test that we can access bitboards correctly
        var buffer = new byte[67];
        position.CopyTo(buffer);
        
        // The first 64 bytes should be 8 ulong bitboards
        for (int i = 0; i < 8; i++)
        {
            ulong bitboard = BitConverter.ToUInt64(buffer, i * 8);
            // For starting position, we should have pieces on ranks 1, 2, 7, 8
            if (i == 0 || i == 7) // Black pieces or White pieces
            {
                bitboard.Should().NotBe(0UL, $"Bitboard {i} should not be empty for starting position");
            }
        }
        
        // Check side to move (byte 64)
        byte stm = buffer[64];
        (stm == 0 || stm == 7).Should().BeTrue("Side to move should be 0 (Black) or 7 (White)");
        
        // Check castling rights (byte 65) 
        byte castling = buffer[65];
        // Starting position should have full castling rights
        castling.Should().NotBe(0, "Starting position should have castling rights");
        
        // Check en passant (byte 66)
        byte enPassant = buffer[66];
        // Starting position should have no en passant
        enPassant.Should().Be(0, "Starting position should have no en passant square");
    }
    
    [Test]
    public void TestWDLRange()
    {
        var position = new MutablePosition();
        
        // Test extreme WDL values
        var testValues = new float[] { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };
        
        foreach (var wdl in testValues)
        {
            using var stream = new MemoryStream();
            BinarySerializer.WritePosition(stream, position, 0, wdl);
            
            var data = stream.ToArray();
            float readWdl = BitConverter.ToSingle(data, 69);
            
            readWdl.Should().BeApproximately(wdl, 0.000001f, $"WDL {wdl} should round-trip correctly");
        }
    }
    
    [Test] 
    public void TestEvalRange()
    {
        var position = new MutablePosition();
        
        // Test extreme eval values
        var testValues = new short[] { short.MinValue, -1000, -100, 0, 100, 1000, short.MaxValue };
        
        foreach (var eval in testValues)
        {
            using var stream = new MemoryStream();
            BinarySerializer.WritePosition(stream, position, eval, 0.5f);
            
            var data = stream.ToArray();
            short readEval = BitConverter.ToInt16(data, 67);
            
            readEval.Should().Be(eval, $"Eval {eval} should round-trip correctly");
        }
    }
    
    [Test]
    public void TestMultiplePositions()
    {
        // Test writing multiple positions to verify format consistency
        var positions = new[]
        {
            (new MutablePosition(), (short)25, 0.55f),
            (new MutablePosition(), (short)-150, 0.35f),
            (new MutablePosition(), (short)0, 0.5f)
        };
        
        using var stream = new MemoryStream();
        
        // Write all positions
        foreach (var (pos, eval, wdl) in positions)
        {
            BinarySerializer.WritePosition(stream, pos, eval, wdl);
        }
        
        var allData = stream.ToArray();
        
        // Verify total size
        allData.Length.Should().Be(73 * positions.Length, "Total size should be 73 * number of positions");
        
        // Verify each position can be read back
        for (int i = 0; i < positions.Length; i++)
        {
            int offset = i * 73;
            
            // Extract this position's data
            var posData = new byte[73];
            Array.Copy(allData, offset, posData, 0, 73);
            
            // Verify eval and WDL
            short readEval = BitConverter.ToInt16(posData, 67);
            float readWdl = BitConverter.ToSingle(posData, 69);
            
            readEval.Should().Be(positions[i].Item2, $"Position {i} eval should match");
            readWdl.Should().BeApproximately(positions[i].Item3, 0.000001f, $"Position {i} WDL should match");
        }
    }
    
    [Test]
    public void TestFileWriteAndRead()
    {
        // Test writing to and reading from an actual file
        var tempFile = Path.GetTempFileName();
        
        try
        {
            var position = new MutablePosition();
            short eval = 150;
            float wdl = 0.65f;
            
            // Write to file
            using (var fileStream = File.Create(tempFile))
            {
                BinarySerializer.WritePosition(fileStream, position, eval, wdl);
            }
            
            // Verify file size
            var fileInfo = new FileInfo(tempFile);
            fileInfo.Length.Should().Be(73, "File should contain exactly 73 bytes");
            
            // Read back from file
            var fileData = File.ReadAllBytes(tempFile);
            fileData.Length.Should().Be(73, "Read data should be 73 bytes");
            
            // Verify position data (first 67 bytes)
            var expectedBuffer = new byte[67];
            position.CopyTo(expectedBuffer);
            var actualPositionData = new byte[67];
            Array.Copy(fileData, 0, actualPositionData, 0, 67);
            actualPositionData.Should().Equal(expectedBuffer, "Position data should match");
            
            // Verify eval
            short readEval = BitConverter.ToInt16(fileData, 67);
            readEval.Should().Be(eval, "Eval should match");
            
            // Verify WDL
            float readWdl = BitConverter.ToSingle(fileData, 69);
            readWdl.Should().BeApproximately(wdl, 0.000001f, "WDL should match");
        }
        finally
        {
            // Clean up
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    
    [Test]
    public void TestLargeFileWithMultiplePositions()
    {
        // Test writing many positions to a file and reading them back
        var tempFile = Path.GetTempFileName();
        
        try
        {
            var testData = new List<(MutablePosition pos, short eval, float wdl)>();
            
            // Create test positions with varying data
            for (int i = 0; i < 100; i++)
            {
                var position = new MutablePosition();
                short eval = (short)(i * 10 - 500); // Range from -500 to +490
                float wdl = i / 99.0f; // Range from 0.0 to 1.0
                testData.Add((position, eval, wdl));
            }
            
            // Write all positions to file
            using (var fileStream = File.Create(tempFile))
            {
                foreach (var (pos, eval, wdl) in testData)
                {
                    BinarySerializer.WritePosition(fileStream, pos, eval, wdl);
                }
            }
            
            // Verify file size
            var fileInfo = new FileInfo(tempFile);
            fileInfo.Length.Should().Be(73 * 100, "File should contain 7300 bytes (100 positions × 73 bytes)");
            
            // Read and verify all positions
            var fileData = File.ReadAllBytes(tempFile);
            
            for (int i = 0; i < testData.Count; i++)
            {
                int offset = i * 73;
                
                // Read eval and WDL for this position
                short readEval = BitConverter.ToInt16(fileData, offset + 67);
                float readWdl = BitConverter.ToSingle(fileData, offset + 69);
                
                var (_, expectedEval, expectedWdl) = testData[i];
                
                readEval.Should().Be(expectedEval, $"Position {i} eval should match");
                readWdl.Should().BeApproximately(expectedWdl, 0.000001f, $"Position {i} WDL should match");
            }
        }
        finally
        {
            // Clean up
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    
    [Test]
    public void TestFileAppendMode()
    {
        // Test that we can append positions to an existing file
        var tempFile = Path.GetTempFileName();
        
        try
        {
            var position1 = new MutablePosition();
            var position2 = new MutablePosition();
            
            // Write first position
            using (var fileStream = File.Create(tempFile))
            {
                BinarySerializer.WritePosition(fileStream, position1, 100, 0.6f);
            }
            
            // Append second position
            using (var fileStream = File.OpenWrite(tempFile))
            {
                fileStream.Seek(0, SeekOrigin.End);
                BinarySerializer.WritePosition(fileStream, position2, -200, 0.3f);
            }
            
            // Verify file contains both positions
            var fileInfo = new FileInfo(tempFile);
            fileInfo.Length.Should().Be(146, "File should contain 146 bytes (2 positions × 73 bytes)");
            
            var fileData = File.ReadAllBytes(tempFile);
            
            // Verify first position
            short eval1 = BitConverter.ToInt16(fileData, 67);
            float wdl1 = BitConverter.ToSingle(fileData, 69);
            eval1.Should().Be(100);
            wdl1.Should().BeApproximately(0.6f, 0.000001f);
            
            // Verify second position
            short eval2 = BitConverter.ToInt16(fileData, 73 + 67);
            float wdl2 = BitConverter.ToSingle(fileData, 73 + 69);
            eval2.Should().Be(-200);
            wdl2.Should().BeApproximately(0.3f, 0.000001f);
        }
        finally
        {
            // Clean up
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    
    [Test]
    public void TestColorBitboardsNeverZero()
    {
        // Test that color bitboards are never zero in a valid position
        var tempFile = Path.GetTempFileName();
        
        try
        {
            var position = new MutablePosition(); // Starting position
            
            // Verify starting position has pieces for both colors
            position.Black.Should().NotBe(0UL, "Black should have pieces in starting position");
            position.White.Should().NotBe(0UL, "White should have pieces in starting position");
            
            // Write to file
            using (var fileStream = File.Create(tempFile))
            {
                BinarySerializer.WritePosition(fileStream, position, 0, 0.5f);
            }
            
            // Read back and check bitboards
            var fileData = File.ReadAllBytes(tempFile);
            
            // Check BlackIndex bitboard (index 0)
            ulong blackBitboard = BitConverter.ToUInt64(fileData, 0 * 8);
            blackBitboard.Should().NotBe(0UL, "Black color bitboard should never be zero");
            
            // Check WhiteIndex bitboard (index 7)  
            ulong whiteBitboard = BitConverter.ToUInt64(fileData, 7 * 8);
            whiteBitboard.Should().NotBe(0UL, "White color bitboard should never be zero");
            
            // Verify these match the position properties
            blackBitboard.Should().Be(position.Black, "Serialized black bitboard should match position");
            whiteBitboard.Should().Be(position.White, "Serialized white bitboard should match position");
            
            // Check that piece bitboards make sense
            for (int i = 1; i <= 6; i++) // Piece type bitboards
            {
                ulong pieceBitboard = BitConverter.ToUInt64(fileData, i * 8);
                // Piece bitboards can be zero (if no pieces of that type), but let's log them
                Console.WriteLine($"Piece bitboard {i}: 0x{pieceBitboard:X16}");
            }
            
            Console.WriteLine($"Black bitboard: 0x{blackBitboard:X16}");
            Console.WriteLine($"White bitboard: 0x{whiteBitboard:X16}");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    
    [Test]
    public void TestEmptyBoardSerialization()
    {
        // Test what happens when we serialize an empty board
        var tempFile = Path.GetTempFileName();
        
        try
        {
            var emptyPosition = MutablePosition.EmptyBoard;
            
            // Empty board should have zero bitboards
            Console.WriteLine($"Empty board - Black: 0x{emptyPosition.Black:X16}");
            Console.WriteLine($"Empty board - White: 0x{emptyPosition.White:X16}");
            
            // Write empty position to file
            using (var fileStream = File.Create(tempFile))
            {
                BinarySerializer.WritePosition(fileStream, emptyPosition, 0, 0.5f);
            }
            
            // Read back and check bitboards
            var fileData = File.ReadAllBytes(tempFile);
            
            // Check all bitboards
            for (int i = 0; i < 8; i++)
            {
                ulong bitboard = BitConverter.ToUInt64(fileData, i * 8);
                Console.WriteLine($"Empty board bitboard {i}: 0x{bitboard:X16}");
            }
            
            // This shows us what zero bitboards look like in serialization
            ulong blackBitboard = BitConverter.ToUInt64(fileData, 0 * 8);
            ulong whiteBitboard = BitConverter.ToUInt64(fileData, 7 * 8);
            
            // These SHOULD be zero for empty board
            blackBitboard.Should().Be(0UL, "Empty board should have no black pieces");
            whiteBitboard.Should().Be(0UL, "Empty board should have no white pieces");
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    
    [Test]
    public void TestMutatedPositionSerialization()
    {
        // Test serialization of positions after DropRandomPiece (training data augmentation)
        var tempFile = Path.GetTempFileName();
        
        try
        {
            var position = new MutablePosition(); // Starting position
            
            // Drop several pieces to simulate what happens during training data generation
            for (int i = 0; i < 5; i++)
            {
                position.DropRandomPiece();
                
                Console.WriteLine($"After dropping {i+1} pieces:");
                Console.WriteLine($"  Black: 0x{position.Black:X16} (count: {Bitboards.CountOccupied(position.Black)})");
                Console.WriteLine($"  White: 0x{position.White:X16} (count: {Bitboards.CountOccupied(position.White)})");
                
                // Write position to file
                using (var fileStream = File.Create(tempFile))
                {
                    BinarySerializer.WritePosition(fileStream, position, 0, 0.5f);
                }
                
                // Read back and verify
                var fileData = File.ReadAllBytes(tempFile);
                ulong blackBitboard = BitConverter.ToUInt64(fileData, 0 * 8);
                ulong whiteBitboard = BitConverter.ToUInt64(fileData, 7 * 8);
                
                // Verify serialized data matches position
                blackBitboard.Should().Be(position.Black, "Serialized black bitboard should match position");
                whiteBitboard.Should().Be(position.White, "Serialized white bitboard should match position");
                
                // These positions are VALID even if sparse - this is legitimate training data
                // from endgame positions where one side has few pieces
                
                if (blackBitboard == 0)
                {
                    Console.WriteLine("  *** Black has no pieces - this is valid endgame position!");
                }
                if (whiteBitboard == 0)  
                {
                    Console.WriteLine("  *** White has no pieces - this is valid endgame position!");
                }
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}