using System;
using System.Security.Cryptography;
using System.Text;

namespace Bot.Helpers;

public static class DataProtectionHelper
{    
    public static string? Base64Encode(string? text) 
    {
        try
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(text!);
            return Convert.ToBase64String(textBytes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? Base64Decode(string? text) 
    {
        try
        {
            byte[] textBytes = Convert.FromBase64String(text!);
            return Encoding.UTF8.GetString(textBytes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? EncryptDataAsText(string? data, string? entropy, bool isUserScope = true)
    {
        try
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data!);
            byte[]? entropyBytes = entropy != null ? Encoding.UTF8.GetBytes(entropy) : null;
            byte[] encryptedData = ProtectedData.Protect(dataBytes, entropyBytes, isUserScope ? DataProtectionScope.CurrentUser : DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(encryptedData);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string? DecryptDataAsText(string? data, string? entropy, bool isUserScope = true)
    {
        try
        {
            byte[] dataBytes = Convert.FromBase64String(data!);
            byte[]? entropyBytes = entropy != null ? Encoding.UTF8.GetBytes(entropy) : null;
            byte[] decryptedData = ProtectedData.Unprotect(dataBytes, entropyBytes, isUserScope ? DataProtectionScope.CurrentUser : DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(decryptedData);
        }
        catch (Exception)
        {
            return null;
        }
    }
}