using System.Security.Cryptography;
using System.Text;

namespace Marbles;

/// <summary>
/// Read-only support for data files written by Marbles v1.0.1.3 and earlier (file format version 1).
/// These files are a single Base64 string holding the AES-256-CBC encrypted document, using a fixed IV and
/// the password itself (repeated up to 32 characters) as the key. Nothing is ever written in this format:
/// a legacy file is converted to the current format when logging in (see FileIo.ConvertLegacyFile).
/// </summary>
internal static class LegacyEncryptor
{
    private const string Iv = "0pN-391xnqr2a5dv"; // Hardcoded in the legacy format: DO NOT CHANGE
    private const int KeyMaxLength = 32;
    private const int KeyMinLength = 8;

    // "Fill up" user password if under 32 chars
    // The key will always be 32 chars long
    private static string GenerateKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > KeyMaxLength)
        {
            return string.Empty;
        }

        while (key.Length < KeyMaxLength)
        {
            key += key;
        }

        return key[..KeyMaxLength];
    }

    /// <summary>
    /// Decrypt the content of a legacy data file
    /// </summary>
    /// <param name="fileContent">The complete content of the data file</param>
    /// <param name="password">The password supplied by the user</param>
    /// <returns>The decrypted string, or DialogText.InvalidData if the password is wrong or the file is not valid</returns>
    internal static string DecryptData(byte[] fileContent, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(password) || password.Length < KeyMinLength)
            {
                return DialogText.InvalidData;
            }

            var textbytes = Convert.FromBase64String(Encoding.UTF8.GetString(fileContent).Trim('﻿', ' ', '\r', '\n'));
            using var endec = Aes.Create();
            endec.BlockSize = 128;
            endec.KeySize = 256;
            endec.IV = Encoding.ASCII.GetBytes(Iv);
            endec.Key = Encoding.ASCII.GetBytes(GenerateKey(password)); // ASCII is part of the legacy format
            endec.Padding = PaddingMode.PKCS7;
            endec.Mode = CipherMode.CBC;
            using var icrypt = endec.CreateDecryptor(endec.Key, endec.IV);
            var decrypted = Encoding.UTF8.GetString(icrypt.TransformFinalBlock(textbytes, 0, textbytes.Length));
            return ValidateDecryptedContent(decrypted) ? decrypted : DialogText.InvalidData;
        }
        catch
        {
            return DialogText.InvalidData;
        }
    }

    // The legacy format has no integrity check, so the content is only accepted
    // if it contains the XML-doctype or is "nodata" (as in new empty data file)
    private static bool ValidateDecryptedContent(string s)
    {
        return Tools.IsValid(s) && (s.Contains(FileIo.XmlDoctype) || s == DialogText.NoData);
    }
}
