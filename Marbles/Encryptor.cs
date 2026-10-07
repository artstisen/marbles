using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Marbles;

class Encryptor
{
    // Data file format (version 2). All numbers are big-endian:
    //
    //   Offset  Length  Content
    //   0       4       Magic "MBLS"
    //   4       1       File format version (2)
    //   5       1       Key derivation id (1 = PBKDF2-HMAC-SHA256)
    //   6       4       Key derivation iterations
    //   10      16      Salt (random, renewed whenever a key is derived for saving)
    //   26      12      Nonce (random, renewed on every save)
    //   38      16      Authentication tag
    //   54      n       Ciphertext (AES-256-GCM of the UTF-8 encoded document)
    //
    // The first 38 bytes (everything before the tag) are authenticated as associated data,
    // so neither the header nor the content can be altered without the load failing.
    // Files written by earlier versions have no header and are handled by LegacyEncryptor.
    private static readonly byte[] Magic = { (byte)'M', (byte)'B', (byte)'L', (byte)'S' }; // DO NOT CHANGE
    private const byte FormatVersion = 2;
    private const byte KdfPbkdf2Sha256 = 1;
    private const int KdfIterations = 600000; // Used for new keys. Files keep working if this value is raised later
    private const int KdfMinIterations = 100000; // Reject files claiming less (or absurdly more) than this
    private const int KdfMaxIterations = 10000000;
    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int KeyLength = 32; // AES-256
    private const int IterationsOffset = 6;
    private const int SaltOffset = 10;
    private const int NonceOffset = SaltOffset + SaltLength;
    private const int TagOffset = NonceOffset + NonceLength; // Also the length of the authenticated header
    private const int HeaderLength = TagOffset + TagLength;

    internal const int KeyMaxLength = 128;
    private const int KeyMinLength = 8;
    private static string _key = string.Empty; // Secret key (password) supplied by the user

    // The derived key is cached together with the salt and iterations it belongs to. Deriving a key is slow
    // by design, so it is only done when logging in or changing password and not on every save.
    private static byte[]? _derivedKey;
    private static byte[] _salt = Array.Empty<byte>();
    private static int _iterations;

    /// <summary>
    /// True if the last call to DecryptData was given a data file from before file format version 2
    /// </summary>
    internal static bool LegacyFormatDetected { get; private set; }

    internal static void SetKey(string key)
    {
        _key = key;
        ClearDerivedKey();
    }

    /// <summary>
    /// Compare a password to the one currently in use
    /// </summary>
    internal static bool IsCurrentKey(string key)
    {
        return Tools.IsValid(_key) && key == _key;
    }

    internal static void RemoveKey()
    {
        _key = DialogText.Cleared;
        _key = string.Empty;
        ClearDerivedKey();
    }

    private static void ClearDerivedKey()
    {
        if (_derivedKey != null)
        {
            CryptographicOperations.ZeroMemory(_derivedKey);
        }

        _derivedKey = null;
        _salt = Array.Empty<byte>();
        _iterations = 0;
    }

    /// <summary>
    /// Validate password length is between 8 and 128 chars long
    /// </summary>
    /// <param name="password">The password string to test</param>
    /// <returns>true or false</returns>
    internal static bool IsValidPasswordLength(string password)
    {
        return password.Length is >= KeyMinLength and <= KeyMaxLength;
    }

    #region Encrypt/decrypt data

    private static byte[] DeriveKey(string password, byte[] salt, int iterations)
    {
        // The password is UTF-8 encoded, so all characters count (not only ASCII)
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeyLength);
    }

    /// <summary>
    /// Encrypt string
    /// </summary>
    /// <param name="decrypted">The input to encrypt</param>
    /// <returns>The complete data file content, or null if the input could not be encrypted</returns>
    internal static byte[]? EncryptData(string decrypted)
    {
        try
        {
            if (string.IsNullOrEmpty(_key))
            {
                return null;
            }

            // Derive a key with a fresh salt if there is none, or if it was derived using outdated settings
            if (_derivedKey == null || _iterations != KdfIterations)
            {
                var salt = RandomNumberGenerator.GetBytes(SaltLength);
                var key = DeriveKey(_key, salt, KdfIterations);
                ClearDerivedKey();
                _derivedKey = key;
                _salt = salt;
                _iterations = KdfIterations;
            }

            var textbytes = Encoding.UTF8.GetBytes(decrypted);
            var output = new byte[HeaderLength + textbytes.Length];
            // Header
            Magic.CopyTo(output, 0);
            output[4] = FormatVersion;
            output[5] = KdfPbkdf2Sha256;
            BinaryPrimitives.WriteInt32BigEndian(output.AsSpan(IterationsOffset), _iterations);
            _salt.CopyTo(output, SaltOffset);
            RandomNumberGenerator.Fill(output.AsSpan(NonceOffset, NonceLength)); // Never reuse a nonce with the same key
            // Content
            using var endec = new AesGcm(_derivedKey, TagLength);
            endec.Encrypt(output.AsSpan(NonceOffset, NonceLength), textbytes, output.AsSpan(HeaderLength),
                output.AsSpan(TagOffset, TagLength), output.AsSpan(0, TagOffset));
            return output;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decrypt data file content
    /// </summary>
    /// <param name="encrypted">The complete content of the data file</param>
    /// <returns>The decrypted string, or DialogText.InvalidData if the password is wrong or the file is damaged</returns>
    internal static string DecryptData(byte[] encrypted)
    {
        LegacyFormatDetected = false;
        try
        {
            if (string.IsNullOrEmpty(_key))
            {
                return DialogText.InvalidData;
            }

            // Files without the header are from before file format version 2
            if (!HasHeader(encrypted))
            {
                var legacyContent = LegacyEncryptor.DecryptData(encrypted, _key);
                LegacyFormatDetected = legacyContent != DialogText.InvalidData;
                return legacyContent;
            }

            if (encrypted.Length < HeaderLength || encrypted[4] != FormatVersion || encrypted[5] != KdfPbkdf2Sha256)
            {
                return DialogText.InvalidData;
            }

            var iterations = BinaryPrimitives.ReadInt32BigEndian(encrypted.AsSpan(IterationsOffset));
            if (iterations is < KdfMinIterations or > KdfMaxIterations)
            {
                return DialogText.InvalidData;
            }

            // Reuse the cached key if it belongs to this file, else derive it
            var salt = encrypted.AsSpan(SaltOffset, SaltLength).ToArray();
            var key = _derivedKey != null && iterations == _iterations && salt.AsSpan().SequenceEqual(_salt)
                ? _derivedKey
                : DeriveKey(_key, salt, iterations);

            var textbytes = new byte[encrypted.Length - HeaderLength];
            using var endec = new AesGcm(key, TagLength);
            // Throws if the password is wrong or if any part of the file was altered
            endec.Decrypt(encrypted.AsSpan(NonceOffset, NonceLength), encrypted.AsSpan(HeaderLength),
                encrypted.AsSpan(TagOffset, TagLength), textbytes, encrypted.AsSpan(0, TagOffset));

            // Keep the key for the following saves
            if (!ReferenceEquals(key, _derivedKey))
            {
                ClearDerivedKey();
                _derivedKey = key;
                _salt = salt;
                _iterations = iterations;
            }

            return Encoding.UTF8.GetString(textbytes);
        }
        catch
        {
            return DialogText.InvalidData;
        }
    }

    private static bool HasHeader(byte[] content)
    {
        return content.Length >= Magic.Length && content.AsSpan(0, Magic.Length).SequenceEqual(Magic);
    }

    #endregion
}
