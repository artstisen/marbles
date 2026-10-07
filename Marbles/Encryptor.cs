using System.Security.Cryptography;
using System.Text;

namespace marbles;

class Encryptor
{
    private const string Iv = "0pN-391xnqr2a5dv"; // Salt (hardcoded in application: DO NOT CHANGE)
    private const int KeyMaxLength = 32;
    private const int KeyMinLength = 8;
    private static string _key = string.Empty; // Secret key supplied by the user

    internal static void SetKey(string key)
    {
        _key = key;
    }

    internal static string GetKey()
    {
        return _key;
    }

    internal static void RemoveKey()
    {
        _key = DialogText.Cleared;
        _key = string.Empty;
    }

    /// <summary>
    /// Validate password length is between 8 and 32 chars long
    /// </summary>
    /// <param name="password">The password string to test</param>
    /// <returns>true or false</returns>
    internal static bool IsValidPasswordLength(string password)
    {
        return password.Length is >= KeyMinLength and <= KeyMaxLength;
    }

    #region Encrypt/decrypt data

    // Private method used to "fill up" user password if under 32 chars
    // The key will always be 32 chars long
    private static string GenerateKey(string key)
    {
        switch (string.IsNullOrEmpty(key))
        {
            case false when key.Length < KeyMaxLength:
            {
                while (key.Length < KeyMaxLength)
                {
                    key += key;
                }

                if (key.Length >= KeyMaxLength)
                {
                    key = key[..KeyMaxLength];
                }

                return key;
            }
            case false when key.Length == KeyMaxLength:
                return key;
            default:
                return string.Empty;
        }
    }


    private static string _tempKey = string.Empty;

    //private static string _tempMessage = string.Empty; // Debugging var
    /// <summary>
    /// Encrypt string
    /// </summary>
    /// <param name="decrypted">The input to encrypt</param>
    /// <returns></returns>
    internal static string EncryptData(string decrypted)
    {
        try
        {
            if (!string.IsNullOrEmpty(_key))
            {
                _tempKey = GenerateKey(_key);
                var textbytes = Encoding.UTF8.GetBytes(decrypted);
                var endec = Aes.Create();
                endec.BlockSize = 128;
                endec.KeySize = 256;
                endec.IV = Encoding.ASCII.GetBytes(Iv);
                endec.Key = Encoding.ASCII.GetBytes(_tempKey);
                endec.Padding = PaddingMode.PKCS7;
                endec.Mode = CipherMode.CBC;
                var icrypt = endec.CreateEncryptor(endec.Key, endec.IV);
                var enc = icrypt.TransformFinalBlock(textbytes, 0, textbytes.Length);
                icrypt.Dispose();
                return Convert.ToBase64String(enc);
            }
            else
            {
                return DialogText.InvalidData;
            }
        }
        catch // (Exception e)
        {
            //_tempMessage = e.Message;
            return DialogText.InvalidData;
        }
    }

    /// <summary>
    /// Decrypt string
    /// </summary>
    /// <param name="encrypted">The encrypted string</param>
    /// <returns></returns>
    internal static string DecryptData(string encrypted)
    {
        try
        {
            if (!string.IsNullOrEmpty(_key) && _key.Length > 7)
            {
                _tempKey = GenerateKey(_key);
                var textbytes = Convert.FromBase64String(encrypted);
                var endec = Aes.Create();
                endec.BlockSize = 128;
                endec.KeySize = 256;
                endec.IV = Encoding.ASCII.GetBytes(Iv);
                endec.Key = Encoding.ASCII.GetBytes(_tempKey);
                endec.Padding = PaddingMode.PKCS7;
                endec.Mode = CipherMode.CBC;
                var icrypt = endec.CreateDecryptor(endec.Key, endec.IV);
                var enc = icrypt.TransformFinalBlock(textbytes, 0, textbytes.Length);
                icrypt.Dispose();
                if (ValidateDecryptedContent(Encoding.UTF8.GetString(enc)))
                {
                    return Encoding.UTF8.GetString(enc);
                }
            }
            return DialogText.InvalidData;
        }
        catch
        {
            return DialogText.InvalidData;
        }
    }

    // Is valid if contains XML-doctype or "nodata" (as in new empty data file)
    private static bool ValidateDecryptedContent(string s)
    {
        if (Tools.IsValid(s) && (s.Contains(FileIo.XmlDoctype) || s == DialogText.NoData))
        {
            return true;
        }
        return false;
    }

    #endregion
}