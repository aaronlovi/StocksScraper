using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;

namespace Stocks.Persistence;

public static class Encryption {
    public static byte[] Encrypt(string password, long salt, byte[] payload) {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentOutOfRangeException(nameof(password));
        if (salt == 0)
            throw new ArgumentOutOfRangeException(nameof(salt), "Salt cannot be 0");
        ArgumentNullException.ThrowIfNull(nameof(payload));
        if (payload.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload cannot be empty");

        Span<byte> saltSpan = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(saltSpan, salt);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Padding = PaddingMode.PKCS7;

        // Derive the key from the password and salt span
        aes.Key = Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), saltSpan, 2, HashAlgorithmName.SHA256, 32);
        aes.IV = Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), saltSpan, 2, HashAlgorithmName.SHA256, 16);
        using var ms = new MemoryStream();

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            cs.Write(payload, 0, payload.Length);
        return ms.ToArray();
    }

    public static byte[] Decrypt(string password, long salt, byte[] encrypted) {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentOutOfRangeException(nameof(password));
        if (salt == 0)
            throw new ArgumentOutOfRangeException(nameof(salt), "Salt cannot be 0");
        ArgumentNullException.ThrowIfNull(nameof(encrypted));
        if (encrypted.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(encrypted), "Encrypted bytes cannot be empty");

        Span<byte> saltSpan = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(saltSpan, salt);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Padding = PaddingMode.PKCS7;

        // Derive the key from the password and salt span
        aes.Key = Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), saltSpan, 2, HashAlgorithmName.SHA256, 32);
        aes.IV = Rfc2898DeriveBytes.Pbkdf2(password.AsSpan(), saltSpan, 2, HashAlgorithmName.SHA256, 16);
        using var ms = new MemoryStream();

        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            cs.Write(encrypted, 0, encrypted.Length);
        return ms.ToArray();
    }
}
