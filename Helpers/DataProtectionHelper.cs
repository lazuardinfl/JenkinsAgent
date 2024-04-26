using System;
using System.IO;
using System.Security.Cryptography;

namespace Bot.Helpers;

public static class DataProtectionHelper
{
    public static byte[] CreateRandomEntropy()
    {
        // Create a byte array to hold the random value.
        byte[] entropy = new byte[16];

        // Create a new instance of the RandomNumberGenerator.
        // Fill the array with a random value.
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(entropy);
        }

        // Return the array.
        return entropy;
    }

    public static int EncryptDataToStream(byte[] Buffer, byte[] Entropy, DataProtectionScope Scope, Stream S)
    {
        ArgumentNullException.ThrowIfNull(Buffer);
        if (Buffer.Length <= 0)
            throw new ArgumentException("The buffer length was 0.", nameof(Buffer));
        ArgumentNullException.ThrowIfNull(Entropy);
        if (Entropy.Length <= 0)
            throw new ArgumentException("The entropy length was 0.", nameof(Entropy));
        ArgumentNullException.ThrowIfNull(S);

        int length = 0;

        // Encrypt the data and store the result in a new byte array. The original data remains unchanged.
        byte[] encryptedData = ProtectedData.Protect(Buffer, Entropy, Scope);

        // Write the encrypted data to a stream.
        if (S.CanWrite && encryptedData != null)
        {
            S.Write(encryptedData, 0, encryptedData.Length);

            length = encryptedData.Length;
        }

        // Return the length that was written to the stream.
        return length;
    }

    public static byte[] DecryptDataFromStream(byte[] Entropy, DataProtectionScope Scope, Stream S, int Length)
    {
        ArgumentNullException.ThrowIfNull(S);
        if (Length <= 0 )
            throw new ArgumentException("The given length was 0.", nameof(Length));
        ArgumentNullException.ThrowIfNull(Entropy);
        if (Entropy.Length <= 0)
            throw new ArgumentException("The entropy length was 0.", nameof(Entropy));

        byte[] inBuffer = new byte[Length];
        byte[] outBuffer;

        // Read the encrypted data from a stream.
        if (S.CanRead)
        {
            S.Read(inBuffer, 0, Length);

            outBuffer = ProtectedData.Unprotect(inBuffer, Entropy, Scope);
        }
        else
        {
            throw new IOException("Could not read the stream.");
        }

        // Return the decrypted data
        return outBuffer;
    }
}