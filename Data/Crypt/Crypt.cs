using System.Security.Cryptography;
using System;

public static class Crypt
{
    public static string Decrypt(string _text)
    {
        RijndaelManaged _rijndaelCipher = new()
        {
            Mode = CipherMode.CBC,
            Padding = PaddingMode.PKCS7,
            KeySize = 128,
            BlockSize = 128
        };

        string _key = _text[^64..^32];

        _text = _text[..^64];

        byte[] _encryptedData = Convert.FromBase64String(_text);
        byte[] _pwdBytes = System.Text.Encoding.UTF8.GetBytes(_key);
        byte[] _keyBytes = new byte[16];

        int _len = _pwdBytes.Length;

        if (_len > _keyBytes.Length) _len = _keyBytes.Length;

        Array.Copy(_pwdBytes, _keyBytes, _len);

        _rijndaelCipher.Key = _keyBytes;
        _rijndaelCipher.IV = _keyBytes;

        byte[] _plainText = _rijndaelCipher.CreateDecryptor().TransformFinalBlock(_encryptedData, 0, _encryptedData.Length);

        return $"{System.Text.Encoding.UTF8.GetString(_plainText)}";
    }

    public static string Encrypt(in string _text) // 암호화
    {
        RijndaelManaged _rijndaelCipher = new()
        {
            Mode = CipherMode.CBC,
            Padding = PaddingMode.PKCS7,
            KeySize = 128,
            BlockSize = 128
        };

        string _key = Guid.NewGuid().ToString().Replace("-", "");
        byte[] _pwdBytes = System.Text.Encoding.UTF8.GetBytes(_key);
        byte[] _keyBytes = new byte[16];

        int _len = _pwdBytes.Length;

        if (_len > _keyBytes.Length) _len = _keyBytes.Length;

        Array.Copy(_pwdBytes, _keyBytes, _len);

        _rijndaelCipher.Key = _keyBytes;
        _rijndaelCipher.IV = _keyBytes;

        ICryptoTransform _transform = _rijndaelCipher.CreateEncryptor();

        byte[] _plainText = System.Text.Encoding.UTF8.GetBytes(_text);

        return $"{Convert.ToBase64String(_transform.TransformFinalBlock(_plainText, 0, _plainText.Length))}{_key}{Guid.NewGuid().ToString().Replace("-", "")}";
    }
}
