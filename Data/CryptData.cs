using System.Security.Cryptography;
using System;
using UnityEngine;
using System.IO;

public class CryptData
{
    private readonly Crypt CRYPT = new();

    public void Save<T>(T _data, string _path, CryptType _type = CryptType.Local)
    {
        if (_type == CryptType.Local) _path = $"{Application.persistentDataPath}/{_path}";
        else if (_type == CryptType.Resources) _path = $"Assets/Resources/{_path}";

        File.WriteAllText(_path, CRYPT.Encrypt(JsonUtility.ToJson(_data)), System.Text.Encoding.UTF8);
    }

    public T Load<T>(string _path, CryptType _type = CryptType.Local)
    {
        if (_type == CryptType.Local) _path = $"{Application.persistentDataPath}/{_path}";
        else if (_type == CryptType.Resources) return JsonUtility.FromJson<T>(CRYPT.Decrypt(Resources.Load<TextAsset>(_path).text));

        if (File.Exists(_path)) return JsonUtility.FromJson<T>(CRYPT.Decrypt(File.ReadAllText(_path, System.Text.Encoding.UTF8)));

        return default;
    }

    public void Remove(string _path, CryptType _type = CryptType.Local)
    {
        if (_type == CryptType.Local) _path = $"{Application.persistentDataPath}/{_path}";

        File.Delete(_path);
    }
}

public enum CryptType
{
    Local,
    Resources,
    None
}

public struct Crypt
{
    public readonly string Decrypt(string _text)
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

    public readonly string Encrypt(in string _text) // 암호화
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
